// ============================================================================
// UnityVision MCP Server - Unity Bridge Client (WebSocket-based)
// Communicates with Unity Editor via persistent WebSocket connection
// Unity initiates the connection to this server for maximum reliability
// ============================================================================

import { UnityBridgeError } from './types.js';
import { getWebSocketHub, WebSocketPluginHub } from './websocketHub.js';
import { fileLog } from './fileLogger.js';

// Debug logging
function debugLog(message: string, data?: unknown) {
  const timestamp = new Date().toISOString();
  if (data !== undefined) {
    console.error(`[BridgeClient ${timestamp}] ${message}`, JSON.stringify(data));
  } else {
    console.error(`[BridgeClient ${timestamp}] ${message}`);
  }
  fileLog('DEBUG', 'BridgeClient', message, data);
}

export class UnityBridgeClient {
  private hub: WebSocketPluginHub;
  private timeout: number;

  constructor(timeout: number = 30000) {
    this.hub = getWebSocketHub();
    this.timeout = timeout;
  }

  /**
   * Call a method on the Unity bridge via WebSocket
   * Unity must be connected to the WebSocket hub for this to work
   */
  async call<TParams, TResult>(
    method: string,
    params: TParams,
    projectHash?: string
  ): Promise<TResult> {
    const startTime = Date.now();
    
    debugLog(`Calling method: ${method}`, { params, projectHash });
    
    try {
      // The hub handles session resolution and command dispatch
      const result = await this.hub.sendCommand<TResult>(
        method,
        params as Record<string, unknown>,
        projectHash
      );
      
      const duration = Date.now() - startTime;
      debugLog(`Call completed: ${method} (${duration}ms)`);
      
      return result;
    } catch (error) {
      const duration = Date.now() - startTime;
      const errorMessage = error instanceof Error ? error.message : String(error);
      
      debugLog(`Call failed: ${method} (${duration}ms) - ${errorMessage}`);
      
      // Convert to UnityBridgeError with helpful messages
      if (errorMessage.includes('No Unity instance connected')) {
        throw new UnityBridgeError(
          'NOT_CONNECTED',
          'No Unity instance is connected. Please ensure Unity is running with the UnityVision package installed.',
          {
            method,
            suggestion: 'Open Unity and check Window > UnityVision > Bridge Status to verify the connection.',
            hubStatus: this.hub.getStatus()
          }
        );
      }
      
      if (errorMessage.includes('timed out')) {
        throw new UnityBridgeError(
          'TIMEOUT',
          `Command '${method}' timed out. Unity may be busy (compiling, loading assets, or in a modal dialog).`,
          {
            method,
            timeout: this.timeout,
            suggestion: 'Wait for Unity to finish any ongoing operations and try again.'
          }
        );
      }
      
      if (errorMessage.includes('Multiple Unity instances')) {
        throw new UnityBridgeError(
          'MULTIPLE_INSTANCES',
          errorMessage,
          {
            method,
            sessions: this.hub.getSessions().map(s => ({
              name: s.projectName,
              hash: s.projectHash
            })),
            suggestion: 'Specify which Unity instance to use.'
          }
        );
      }
      
      // Generic error
      throw new UnityBridgeError(
        'COMMAND_FAILED',
        `Command '${method}' failed: ${errorMessage}`,
        {
          method,
          originalError: errorMessage
        }
      );
    }
  }

  /**
   * Check if any Unity instance is connected
   */
  isConnected(): boolean {
    return this.hub.isConnected();
  }

  /**
   * Get the number of connected Unity sessions
   */
  getSessionCount(): number {
    return this.hub.getSessionCount();
  }

  /**
   * Get detailed status information
   */
  getStatus(): object {
    return this.hub.getStatus();
  }

  /**
   * Health check - returns true if at least one Unity is connected
   */
  async healthCheck(): Promise<boolean> {
    return this.hub.isConnected();
  }
}

// Singleton instance
let bridgeClient: UnityBridgeClient | null = null;

export function getBridgeClient(): UnityBridgeClient {
  if (!bridgeClient) {
    const timeout = parseInt(process.env.UNITY_BRIDGE_TIMEOUT || '30000', 10);
    bridgeClient = new UnityBridgeClient(timeout);
  }
  return bridgeClient;
}
