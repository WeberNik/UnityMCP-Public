// ============================================================================
// UnityVision MCP Server - WebSocket Plugin Hub
// Manages persistent WebSocket connections from Unity plugins
// Based on the robust architecture from MCPForUnity
// ============================================================================

import { WebSocketServer, WebSocket, RawData } from 'ws';
import { randomUUID } from 'crypto';
import { IncomingMessage } from 'http';
import { fileLog } from './fileLogger.js';

// Use crypto.randomUUID for UUID generation
const uuidv4 = randomUUID;

// ============================================================================
// Types
// ============================================================================

export interface UnitySession {
  sessionId: string;
  projectName: string;
  projectHash: string;
  unityVersion: string;
  clientName?: string;
  platform?: string;
  connectedAt: Date;
  lastPing: Date;
  socket: WebSocket;
  customTools: Map<string, CustomToolDefinition>;
}

export interface PendingCommand {
  id: string;
  method: string;
  resolve: (result: unknown) => void;
  reject: (error: Error) => void;
  timeout: NodeJS.Timeout;
  startTime: number;
}

interface WelcomeMessage {
  type: 'welcome';
  serverTimeout: number;
  keepAliveInterval: number;
}

interface RegisteredMessage {
  type: 'registered';
  session_id: string;
}

interface ExecuteCommandMessage {
  type: 'execute';
  id: string;
  name: string;
  params: Record<string, unknown>;
  timeout: number;
}

interface CommandResultMessage {
  type: 'command_result';
  id: string;
  result: unknown;
}

interface RegisterMessage {
  type: 'register';
  project_name: string;
  project_hash: string;
  unity_version: string;
  client_name?: string;
  platform?: string;
}

interface PongMessage {
  type: 'pong';
  session_id?: string;
}

interface RegisterToolsMessage {
  type: 'register_tools';
  tools: CustomToolDefinition[];
}

export interface CustomToolDefinition {
  name: string;
  description: string;
  structured_output?: boolean;
  requires_polling?: boolean;
  poll_action?: string;
  parameters?: CustomToolParameter[];
}

export interface CustomToolParameter {
  name: string;
  description: string;
  type: string;
  required: boolean;
  default_value?: unknown;
}

// ============================================================================
// WebSocket Plugin Hub
// ============================================================================

export class WebSocketPluginHub {
  private static instance: WebSocketPluginHub | null = null;
  
  private wss: WebSocketServer | null = null;
  private sessions: Map<string, UnitySession> = new Map();
  private pendingCommands: Map<string, PendingCommand> = new Map();
  private port: number;
  private isRunning: boolean = false;
  
  // Rate limiting
  private rateLimitMap: Map<string, { count: number; windowStart: number }> = new Map();
  
  // Configuration
  private readonly KEEP_ALIVE_INTERVAL = 15000; // 15 seconds
  private readonly SERVER_TIMEOUT = 30000; // 30 seconds
  private readonly COMMAND_TIMEOUT = 30000; // 30 seconds
  private readonly RECONNECT_GRACE_PERIOD = 10000; // 10 seconds to wait for Unity reconnect
  private readonly RATE_LIMIT_WINDOW_MS = 1000; // 1 second window
  private readonly RATE_LIMIT_MAX_REQUESTS = 50; // Max requests per window
  
  private constructor(port: number = 7890) {
    this.port = port;
  }
  
  static getInstance(port?: number): WebSocketPluginHub {
    if (!WebSocketPluginHub.instance) {
      WebSocketPluginHub.instance = new WebSocketPluginHub(port);
    }
    return WebSocketPluginHub.instance;
  }
  
  // ============================================================================
  // Server Lifecycle
  // ============================================================================
  
  async start(): Promise<void> {
    if (this.isRunning) {
      fileLog('WARN', 'WebSocketHub', 'Hub already running');
      return;
    }
    
    // Try to start on the configured port, with fallback to find an available port
    const maxRetries = 10;
    let lastError: Error | null = null;
    
    for (let attempt = 0; attempt < maxRetries; attempt++) {
      const portToTry = this.port + attempt;
      
      try {
        await this.tryStartOnPort(portToTry);
        this.port = portToTry; // Update to actual port used
        return;
      } catch (error) {
        lastError = error as Error;
        const errorMessage = lastError.message || String(lastError);
        
        // Only retry on EADDRINUSE (port already in use)
        if (errorMessage.includes('EADDRINUSE') || errorMessage.includes('address already in use')) {
          fileLog('WARN', 'WebSocketHub', `Port ${portToTry} in use, trying ${portToTry + 1}...`);
          console.error(`[UnityVision] Port ${portToTry} in use, trying next port...`);
          continue;
        }
        
        // For other errors, don't retry
        break;
      }
    }
    
    // All retries failed - but don't crash! Run in "disconnected" mode
    fileLog('WARN', 'WebSocketHub', `Could not bind to any port (tried ${this.port}-${this.port + maxRetries - 1}). Running in disconnected mode.`);
    console.error(`[UnityVision] Warning: Could not start WebSocket server. Another instance may be running.`);
    console.error(`[UnityVision] This instance will work but cannot receive Unity connections directly.`);
    console.error(`[UnityVision] Unity projects should connect to the first MCP server instance.`);
    
    // Mark as "running" but without a server - tools will return graceful errors
    this.isRunning = false;
    this.wss = null;
  }
  
  private tryStartOnPort(port: number): Promise<void> {
    return new Promise((resolve, reject) => {
      try {
        const server = new WebSocketServer({ port });
        
        server.on('listening', () => {
          this.wss = server;
          this.isRunning = true;
          fileLog('INFO', 'WebSocketHub', `WebSocket server started on port ${port}`);
          console.error(`[UnityVision] WebSocket hub listening on ws://localhost:${port}`);
          
          // Set up connection handler
          server.on('connection', (socket: WebSocket, request: IncomingMessage) => {
            this.handleConnection(socket, request);
          });
          
          resolve();
        });
        
        server.on('error', (error: Error) => {
          fileLog('ERROR', 'WebSocketHub', `Server error on port ${port}: ${error.message}`);
          server.close();
          reject(error);
        });
        
      } catch (error) {
        fileLog('ERROR', 'WebSocketHub', `Failed to start on port ${port}: ${(error as Error).message}`);
        reject(error);
      }
    });
  }
  
  stop(): void {
    if (!this.isRunning || !this.wss) {
      return;
    }
    
    // Reject all pending commands
    for (const [id, pending] of this.pendingCommands) {
      clearTimeout(pending.timeout);
      pending.reject(new Error('WebSocket hub shutting down'));
    }
    this.pendingCommands.clear();
    
    // Close all sessions
    for (const [sessionId, session] of this.sessions) {
      try {
        session.socket.close(1001, 'Server shutting down');
      } catch { /* ignore */ }
    }
    this.sessions.clear();
    
    this.wss.close();
    this.wss = null;
    this.isRunning = false;
    
    fileLog('INFO', 'WebSocketHub', 'WebSocket server stopped');
  }
  
  // ============================================================================
  // Connection Handling
  // ============================================================================
  
  private handleConnection(socket: WebSocket, request: IncomingMessage): void {
    const clientIp = request.socket?.remoteAddress || 'unknown';
    fileLog('INFO', 'WebSocketHub', `New connection from ${clientIp}`);
    
    // Send welcome message with server configuration
    const welcome: WelcomeMessage = {
      type: 'welcome',
      serverTimeout: this.SERVER_TIMEOUT / 1000,
      keepAliveInterval: this.KEEP_ALIVE_INTERVAL / 1000,
    };
    socket.send(JSON.stringify(welcome));
    
    // Handle messages
    socket.on('message', async (data: RawData) => {
      try {
        const message = JSON.parse(data.toString());
        await this.handleMessage(socket, message);
      } catch (error) {
        fileLog('ERROR', 'WebSocketHub', `Failed to parse message: ${(error as Error).message}`);
      }
    });
    
    // Handle disconnect
    socket.on('close', (code: number, reason: Buffer) => {
      this.handleDisconnect(socket, code, reason.toString());
    });
    
    socket.on('error', (error: Error) => {
      fileLog('ERROR', 'WebSocketHub', `Socket error: ${error.message}`);
    });
  }
  
  private async handleMessage(socket: WebSocket, message: any): Promise<void> {
    const messageType = message.type;
    
    fileLog('DEBUG', 'WebSocketHub', `Received message type: ${messageType}`);
    
    switch (messageType) {
      case 'register':
        await this.handleRegister(socket, message as RegisterMessage);
        break;
      case 'register_tools':
        this.handleRegisterTools(socket, message as RegisterToolsMessage);
        break;
      case 'pong':
        this.handlePong(message as PongMessage);
        break;
      case 'command_result':
        this.handleCommandResult(message as CommandResultMessage);
        break;
      default:
        fileLog('DEBUG', 'WebSocketHub', `Unknown message type: ${messageType}`);
    }
  }
  
  private async handleRegister(socket: WebSocket, message: RegisterMessage): Promise<void> {
    const sessionId = uuidv4();
    
    const session: UnitySession = {
      sessionId,
      projectName: message.project_name,
      projectHash: message.project_hash,
      unityVersion: message.unity_version,
      clientName: message.client_name,
      platform: message.platform,
      connectedAt: new Date(),
      lastPing: new Date(),
      socket,
      customTools: new Map(),
    };
    
    // Check if this project hash already has a session (reconnection)
    for (const [existingId, existingSession] of this.sessions) {
      if (existingSession.projectHash === message.project_hash) {
        fileLog('INFO', 'WebSocketHub', `Replacing existing session for ${message.project_name}`);
        this.sessions.delete(existingId);
        try {
          existingSession.socket.close(1000, 'Replaced by new connection');
        } catch { /* ignore */ }
        break;
      }
    }
    
    this.sessions.set(sessionId, session);
    
    // Send registered confirmation
    const registered: RegisteredMessage = {
      type: 'registered',
      session_id: sessionId,
    };
    socket.send(JSON.stringify(registered));
    
    fileLog('INFO', 'WebSocketHub', `Registered session ${sessionId} for ${message.project_name} (${message.project_hash})`);
    console.error(`[UnityVision] Unity connected: ${message.project_name} (Unity ${message.unity_version})`);
  }
  
  private handleRegisterTools(socket: WebSocket, message: RegisterToolsMessage): void {
    // Find the session for this socket
    let targetSession: UnitySession | null = null;
    for (const session of this.sessions.values()) {
      if (session.socket === socket) {
        targetSession = session;
        break;
      }
    }
    
    if (!targetSession) {
      fileLog('WARN', 'WebSocketHub', 'Received register_tools from unknown socket');
      return;
    }
    
    // Register the custom tools
    targetSession.customTools.clear();
    for (const tool of message.tools) {
      targetSession.customTools.set(tool.name, tool);
    }
    
    fileLog('INFO', 'WebSocketHub', `Registered ${message.tools.length} custom tools from ${targetSession.projectName}`);
    console.error(`[UnityVision] Registered ${message.tools.length} custom tools from ${targetSession.projectName}`);
  }
  
  private handlePong(message: PongMessage): void {
    if (message.session_id) {
      const session = this.sessions.get(message.session_id);
      if (session) {
        session.lastPing = new Date();
        fileLog('DEBUG', 'WebSocketHub', `Pong received from ${session.projectName}`);
      }
    }
  }
  
  private handleCommandResult(message: CommandResultMessage): void {
    const pending = this.pendingCommands.get(message.id);
    if (!pending) {
      fileLog('WARN', 'WebSocketHub', `Received result for unknown command: ${message.id}`);
      return;
    }
    
    clearTimeout(pending.timeout);
    this.pendingCommands.delete(message.id);
    
    const duration = Date.now() - pending.startTime;
    fileLog('INFO', 'WebSocketHub', `Command ${pending.method} completed in ${duration}ms`);
    
    // Check if result contains an error
    const result = message.result as any;
    if (result && result.status === 'error') {
      pending.reject(new Error(result.error || 'Command failed'));
    } else {
      pending.resolve(result);
    }
  }
  
  private handleDisconnect(socket: WebSocket, code: number, reason: string): void {
    // Find and remove the session
    for (const [sessionId, session] of this.sessions) {
      if (session.socket === socket) {
        this.sessions.delete(sessionId);
        fileLog('INFO', 'WebSocketHub', `Session ${sessionId} disconnected: ${code} - ${reason}`);
        console.error(`[UnityVision] Unity disconnected: ${session.projectName}`);
        
        // Reject any pending commands for this session
        for (const [cmdId, pending] of this.pendingCommands) {
          // We can't easily track which commands belong to which session
          // So we'll let them timeout naturally
        }
        
        return;
      }
    }
  }
  
  // ============================================================================
  // Rate Limiting
  // ============================================================================
  
  /**
   * Check if a request should be rate limited.
   * Returns true if the request is allowed, false if rate limited.
   */
  private checkRateLimit(clientId: string): boolean {
    const now = Date.now();
    const entry = this.rateLimitMap.get(clientId);
    
    if (!entry || now - entry.windowStart >= this.RATE_LIMIT_WINDOW_MS) {
      // New window
      this.rateLimitMap.set(clientId, { count: 1, windowStart: now });
      return true;
    }
    
    if (entry.count >= this.RATE_LIMIT_MAX_REQUESTS) {
      fileLog('WARN', 'WebSocketHub', `Rate limit exceeded for client ${clientId}`);
      return false;
    }
    
    entry.count++;
    return true;
  }
  
  /**
   * Clean up old rate limit entries periodically
   */
  private cleanupRateLimits(): void {
    const now = Date.now();
    for (const [clientId, entry] of this.rateLimitMap) {
      if (now - entry.windowStart >= this.RATE_LIMIT_WINDOW_MS * 10) {
        this.rateLimitMap.delete(clientId);
      }
    }
  }
  
  // ============================================================================
  // Command Execution
  // ============================================================================
  
  async sendCommand<T>(
    method: string,
    params: Record<string, unknown>,
    projectHash?: string
  ): Promise<T> {
    const session = await this.resolveSession(projectHash);
    
    // Rate limit check
    if (!this.checkRateLimit(session.sessionId)) {
      throw new Error('Rate limit exceeded. Please slow down requests.');
    }
    
    const commandId = uuidv4();
    const startTime = Date.now();
    
    fileLog('INFO', 'WebSocketHub', `Sending command ${method} (${commandId}) to ${session.projectName}`);
    
    return new Promise<T>((resolve, reject) => {
      const timeout = setTimeout(() => {
        this.pendingCommands.delete(commandId);
        fileLog('ERROR', 'WebSocketHub', `Command ${method} (${commandId}) timed out after ${this.COMMAND_TIMEOUT}ms`);
        reject(new Error(`Command '${method}' timed out after ${this.COMMAND_TIMEOUT}ms`));
      }, this.COMMAND_TIMEOUT);
      
      const pending: PendingCommand = {
        id: commandId,
        method,
        resolve: resolve as (result: unknown) => void,
        reject,
        timeout,
        startTime,
      };
      
      this.pendingCommands.set(commandId, pending);
      
      const executeMessage: ExecuteCommandMessage = {
        type: 'execute',
        id: commandId,
        name: method,
        params,
        timeout: this.COMMAND_TIMEOUT / 1000,
      };
      
      try {
        session.socket.send(JSON.stringify(executeMessage));
        fileLog('DEBUG', 'WebSocketHub', `Command ${method} sent, waiting for response...`);
      } catch (error) {
        clearTimeout(timeout);
        this.pendingCommands.delete(commandId);
        fileLog('ERROR', 'WebSocketHub', `Failed to send command: ${(error as Error).message}`);
        reject(new Error(`Failed to send command: ${(error as Error).message}`));
      }
    });
  }
  
  private async resolveSession(projectHash?: string): Promise<UnitySession> {
    // Wait for a session to be available (handles Unity startup/reload)
    const maxWaitTime = this.RECONNECT_GRACE_PERIOD;
    const checkInterval = 250;
    let waited = 0;
    
    while (waited < maxWaitTime) {
      // If specific project requested
      if (projectHash) {
        for (const session of this.sessions.values()) {
          if (session.projectHash === projectHash) {
            return session;
          }
        }
      } else {
        // Auto-select if only one session
        if (this.sessions.size === 1) {
          const session = this.sessions.values().next().value;
          if (session) return session;
        }
        if (this.sessions.size > 1) {
          throw new Error(
            `Multiple Unity instances connected. Specify which one to use. ` +
            `Available: ${Array.from(this.sessions.values()).map(s => `${s.projectName}@${s.projectHash}`).join(', ')}`
          );
        }
      }
      
      // No session yet, wait and retry
      if (waited === 0) {
        fileLog('DEBUG', 'WebSocketHub', `No Unity session available, waiting up to ${maxWaitTime}ms...`);
      }
      
      await new Promise(resolve => setTimeout(resolve, checkInterval));
      waited += checkInterval;
    }
    
    throw new Error(
      'No Unity instance connected. Please ensure Unity is running with the UnityVision package installed ' +
      'and the WebSocket client is connected.'
    );
  }
  
  // ============================================================================
  // Status & Info
  // ============================================================================
  
  getSessions(): UnitySession[] {
    return Array.from(this.sessions.values());
  }
  
  getSessionCount(): number {
    return this.sessions.size;
  }
  
  isConnected(): boolean {
    return this.sessions.size > 0;
  }
  
  getPort(): number {
    return this.port;
  }
  
  getStatus(): object {
    return {
      running: this.isRunning,
      port: this.port,
      sessions: Array.from(this.sessions.values()).map(s => ({
        sessionId: s.sessionId,
        projectName: s.projectName,
        projectHash: s.projectHash,
        unityVersion: s.unityVersion,
        clientName: s.clientName,
        platform: s.platform,
        connectedAt: s.connectedAt.toISOString(),
        lastPing: s.lastPing.toISOString(),
        customToolCount: s.customTools.size,
      })),
      pendingCommands: this.pendingCommands.size,
    };
  }
  
  /**
   * Get all custom tools registered by Unity sessions
   */
  getAllCustomTools(): CustomToolDefinition[] {
    const tools: CustomToolDefinition[] = [];
    for (const session of this.sessions.values()) {
      for (const tool of session.customTools.values()) {
        tools.push(tool);
      }
    }
    return tools;
  }
  
  /**
   * Check if a custom tool exists
   */
  hasCustomTool(name: string): boolean {
    for (const session of this.sessions.values()) {
      if (session.customTools.has(name)) {
        return true;
      }
    }
    return false;
  }
  
  /**
   * Get a custom tool definition by name
   */
  getCustomTool(name: string): CustomToolDefinition | undefined {
    for (const session of this.sessions.values()) {
      const tool = session.customTools.get(name);
      if (tool) {
        return tool;
      }
    }
    return undefined;
  }
}

// ============================================================================
// Singleton Export
// ============================================================================

let hubInstance: WebSocketPluginHub | null = null;

export function getWebSocketHub(port?: number): WebSocketPluginHub {
  if (!hubInstance) {
    hubInstance = WebSocketPluginHub.getInstance(port);
  }
  return hubInstance;
}

export async function startWebSocketHub(port: number = 7890): Promise<WebSocketPluginHub> {
  const hub = getWebSocketHub(port);
  await hub.start();
  return hub;
}
