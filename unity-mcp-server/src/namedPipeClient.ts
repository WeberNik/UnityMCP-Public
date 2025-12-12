// ============================================================================
// UnityVision MCP Server - Named Pipe Client
// Connects to Unity via Named Pipes for reliable IPC
// ============================================================================

import * as net from 'net';
import { BridgeRequest, BridgeResponse, UnityBridgeError } from './types.js';
import { fileLog, logRequestStart, logRequestPhase, logRequestEnd, logConnection } from './fileLogger.js';

// Debug logging helper - now also writes to file
function debugLog(message: string, data?: unknown) {
  const timestamp = new Date().toISOString();
  if (data !== undefined) {
    console.error(`[PipeClient ${timestamp}] ${message}`, JSON.stringify(data));
  } else {
    console.error(`[PipeClient ${timestamp}] ${message}`);
  }
  fileLog('DEBUG', 'PipeClient', message, data);
}

/**
 * Named Pipe client for Unity communication.
 * Uses OS-level IPC instead of HTTP for maximum reliability.
 */
export class NamedPipeClient {
  private pipeName: string;
  private socket: net.Socket | null = null;
  private connected: boolean = false;
  private responseBuffer: string = '';
  private pendingRequests: Map<number, {
    resolve: (value: unknown) => void;
    reject: (error: Error) => void;
    timeout: NodeJS.Timeout;
  }> = new Map();
  private requestId: number = 0;
  private connectionTimeout: number;
  private requestTimeout: number;

  constructor(pipeName: string, connectionTimeout = 5000, requestTimeout = 30000) {
    this.pipeName = pipeName;
    this.connectionTimeout = connectionTimeout;
    this.requestTimeout = requestTimeout;
  }

  /**
   * Get the pipe path for the current platform
   */
  private getPipePath(): string {
    if (process.platform === 'win32') {
      return `\\\\.\\pipe\\${this.pipeName}`;
    } else {
      // Unix domain socket fallback
      return `/tmp/${this.pipeName}.sock`;
    }
  }

  /**
   * Connect to the Unity Named Pipe server
   */
  async connect(): Promise<void> {
    if (this.connected && this.socket) {
      logConnection('Already connected to pipe', { pipeName: this.pipeName });
      return;
    }

    const pipePath = this.getPipePath();
    logConnection('Attempting pipe connection', { pipePath, pipeName: this.pipeName });
    debugLog(`Connecting to pipe: ${pipePath}`);

    return new Promise((resolve, reject) => {
      const timeoutId = setTimeout(() => {
        if (this.socket) {
          this.socket.destroy();
          this.socket = null;
        }
        reject(new UnityBridgeError(
          'CONNECTION_TIMEOUT',
          `Failed to connect to Unity pipe within ${this.connectionTimeout}ms`,
          { pipeName: this.pipeName }
        ));
      }, this.connectionTimeout);

      this.socket = net.connect(pipePath);

      this.socket.on('connect', () => {
        clearTimeout(timeoutId);
        this.connected = true;
        logConnection('Pipe connected successfully', { pipeName: this.pipeName });
        debugLog('Connected to Unity pipe');
        resolve();
      });

      this.socket.on('data', (data) => {
        this.handleData(data);
      });

      this.socket.on('error', (error) => {
        clearTimeout(timeoutId);
        this.connected = false;
        debugLog('Pipe error:', error.message);
        
        // Reject all pending requests
        for (const [id, pending] of this.pendingRequests) {
          clearTimeout(pending.timeout);
          pending.reject(new UnityBridgeError(
            'PIPE_ERROR',
            `Pipe error: ${error.message}`,
            { pipeName: this.pipeName }
          ));
        }
        this.pendingRequests.clear();
        
        reject(new UnityBridgeError(
          'CONNECTION_ERROR',
          `Failed to connect to Unity: ${error.message}`,
          { pipeName: this.pipeName }
        ));
      });

      this.socket.on('close', () => {
        this.connected = false;
        this.socket = null;
        debugLog('Pipe connection closed');
      });
    });
  }

  /**
   * Handle incoming data from the pipe
   */
  private handleData(data: Buffer): void {
    this.responseBuffer += data.toString('utf8');
    
    // Process complete lines (newline-delimited JSON)
    let newlineIndex: number;
    while ((newlineIndex = this.responseBuffer.indexOf('\n')) !== -1) {
      const line = this.responseBuffer.substring(0, newlineIndex);
      this.responseBuffer = this.responseBuffer.substring(newlineIndex + 1);
      
      if (line.trim()) {
        this.handleResponse(line);
      }
    }
  }

  /**
   * Handle a complete JSON response
   */
  private handleResponse(json: string): void {
    const responseTime = Date.now();
    fileLog('DEBUG', 'PipeClient', 'RESPONSE_RECEIVED', { jsonLength: json.length, preview: json.substring(0, 200) });
    
    try {
      const response = JSON.parse(json) as BridgeResponse<unknown>;
      
      // For now, we handle responses in order (single request at a time)
      // In the future, we could add request IDs for multiplexing
      const pendingEntry = this.pendingRequests.entries().next().value as [number, typeof this.pendingRequests extends Map<number, infer V> ? V : never] | undefined;
      if (pendingEntry) {
        const [id, pending] = pendingEntry;
        clearTimeout(pending.timeout);
        this.pendingRequests.delete(id);
        
        fileLog('INFO', 'PipeClient', `RESPONSE_PROCESSED for request ${id}`, { 
          hasError: !!response.error,
          resultType: response.result ? typeof response.result : 'none'
        });
        
        if (response.error) {
          pending.reject(new UnityBridgeError(
            response.error.code,
            response.error.message,
            response.error.details
          ));
        } else {
          pending.resolve(response.result);
        }
      } else {
        fileLog('WARN', 'PipeClient', 'RESPONSE_NO_PENDING_REQUEST', { pendingCount: this.pendingRequests.size });
      }
    } catch (error) {
      fileLog('ERROR', 'PipeClient', 'RESPONSE_PARSE_ERROR', { json: json.substring(0, 500), error: (error as Error).message });
      debugLog('Failed to parse response:', json);
    }
  }

  /**
   * Disconnect from the pipe
   */
  disconnect(): void {
    if (this.socket) {
      this.socket.destroy();
      this.socket = null;
    }
    this.connected = false;
    this.responseBuffer = '';
    
    // Reject all pending requests
    for (const [id, pending] of this.pendingRequests) {
      clearTimeout(pending.timeout);
      pending.reject(new UnityBridgeError(
        'DISCONNECTED',
        'Pipe connection closed',
        { pipeName: this.pipeName }
      ));
    }
    this.pendingRequests.clear();
  }

  /**
   * Check if connected
   */
  isConnected(): boolean {
    return this.connected && this.socket !== null;
  }

  /**
   * Call a method on the Unity bridge
   */
  async call<TParams, TResult>(
    method: string,
    params: TParams
  ): Promise<TResult> {
    const callStartTime = Date.now();
    const reqId = `pipe-${++this.requestId}`;
    
    logRequestStart(reqId, method, params);
    logRequestPhase(reqId, 'CALL_START', { connected: this.connected });
    
    // Ensure connected
    if (!this.connected) {
      logRequestPhase(reqId, 'CONNECTING');
      await this.connect();
      logRequestPhase(reqId, 'CONNECTED', { elapsed: Date.now() - callStartTime });
    }

    if (!this.socket) {
      logRequestEnd(reqId, method, Date.now() - callStartTime, false, 'NOT_CONNECTED');
      throw new UnityBridgeError(
        'NOT_CONNECTED',
        'Not connected to Unity pipe',
        { pipeName: this.pipeName }
      );
    }

    const request: BridgeRequest = { method, params };
    const requestJson = JSON.stringify(request) + '\n';
    const id = this.requestId;

    logRequestPhase(reqId, 'SENDING_REQUEST', { jsonLength: requestJson.length });
    debugLog(`Sending request: ${method}`, params);

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.pendingRequests.delete(id);
        logRequestEnd(reqId, method, Date.now() - callStartTime, false, `TIMEOUT after ${this.requestTimeout}ms`);
        reject(new UnityBridgeError(
          'TIMEOUT',
          `Request timed out after ${this.requestTimeout}ms`,
          { method, pipeName: this.pipeName }
        ));
      }, this.requestTimeout);

      this.pendingRequests.set(id, {
        resolve: resolve as (value: unknown) => void,
        reject,
        timeout
      });

      this.socket!.write(requestJson, 'utf8', (error) => {
        if (error) {
          clearTimeout(timeout);
          this.pendingRequests.delete(id);
          logRequestEnd(reqId, method, Date.now() - callStartTime, false, `WRITE_ERROR: ${error.message}`);
          reject(new UnityBridgeError(
            'WRITE_ERROR',
            `Failed to write to pipe: ${error.message}`,
            { method, pipeName: this.pipeName }
          ));
        } else {
          logRequestPhase(reqId, 'REQUEST_SENT_WAITING_RESPONSE', { elapsed: Date.now() - callStartTime });
        }
      });
    });
  }
}

// Singleton instance cache by pipe name
const pipeClients: Map<string, NamedPipeClient> = new Map();

/**
 * Get or create a Named Pipe client for the given pipe name
 */
export function getNamedPipeClient(pipeName: string): NamedPipeClient {
  let client = pipeClients.get(pipeName);
  if (!client) {
    client = new NamedPipeClient(pipeName);
    pipeClients.set(pipeName, client);
  }
  return client;
}

/**
 * Check if a named pipe exists and is connectable
 */
export async function checkPipeHealth(pipeName: string): Promise<boolean> {
  const client = new NamedPipeClient(pipeName, 2000, 5000);
  try {
    await client.connect();
    client.disconnect();
    return true;
  } catch {
    return false;
  }
}
