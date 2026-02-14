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
  sessionId: string;
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
  result?: unknown;
  error?: {
    code?: string;
    message?: string;
    details?: unknown;
  };
}

interface RegisterMessage {
  type: 'register';
  project_name: string;
  project_hash: string;
  unity_version: string;
  client_name?: string;
  platform?: string;
}

interface PingMessage {
  type: 'ping';
  session_id?: string;
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
  private readonly RECONNECT_GRACE_PERIOD = 45000; // 45 seconds to wait for Unity reconnect (assembly reloads can be slow)
  private readonly RATE_LIMIT_WINDOW_MS = 1000; // 1 second window
  private readonly RATE_LIMIT_MAX_REQUESTS = 50; // Max requests per window
  
  // Heartbeat timer for dead session detection
  private keepAliveTimer: NodeJS.Timeout | null = null;
  
  // Circuit breaker state
  private consecutiveFailures: number = 0;
  private circuitBreakerUntil: number = 0;
  private readonly CIRCUIT_BREAKER_THRESHOLD = 3;
  private readonly CIRCUIT_BREAKER_COOLDOWN = 10000; // 10 seconds
  
  // Session waiters for resolveSession
  private sessionWaiters: Array<{ resolve: (session: UnitySession | PromiseLike<UnitySession>) => void; projectHash?: string }> = [];
  
  private constructor(port: number = 6400) {
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
          
          // Start heartbeat timer for dead session detection
          this.startHeartbeat();
          
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
    
    // Stop heartbeat timer
    this.stopHeartbeat();
    
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
      case 'ping':
        this.handlePing(socket, message as PingMessage);
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
    
    // Resolve any pending session waiters immediately (session migration)
    const resolvedWaiters: number[] = [];
    for (let i = 0; i < this.sessionWaiters.length; i++) {
      const waiter = this.sessionWaiters[i];
      if (!waiter.projectHash || waiter.projectHash === message.project_hash) {
        waiter.resolve(session);
        resolvedWaiters.push(i);
      }
    }
    // Remove resolved waiters in reverse order to preserve indices
    for (let i = resolvedWaiters.length - 1; i >= 0; i--) {
      this.sessionWaiters.splice(resolvedWaiters[i], 1);
    }
    if (resolvedWaiters.length > 0) {
      fileLog('INFO', 'WebSocketHub', `Resolved ${resolvedWaiters.length} pending session waiter(s) for ${message.project_name}`);
    }
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
    
    const result = message.result as any;
    const topLevelError = message.error;
    const nestedError = result && result.status === 'error';
    if (topLevelError || nestedError) {
      this.consecutiveFailures++;
      const errorMessage =
        topLevelError?.message ||
        result?.error ||
        `Command '${pending.method}' failed`;
      pending.reject(new Error(errorMessage));
    } else {
      // Success - reset circuit breaker
      if (this.consecutiveFailures > 0) {
        fileLog('INFO', 'WebSocketHub', `Circuit breaker reset - command succeeded after ${this.consecutiveFailures} failure(s)`);
      }
      this.consecutiveFailures = 0;
      this.circuitBreakerUntil = 0;
      pending.resolve(result);
    }
  }
  private handleDisconnect(socket: WebSocket, code: number, reason: string): void {
    // Find and clean up the session, immediately rejecting its pending commands
    for (const [sessionId, session] of this.sessions) {
      if (session.socket === socket) {
        fileLog('INFO', 'WebSocketHub', `Session ${sessionId} disconnected: ${code} - ${reason}`);
        this.cleanupSession(sessionId, session);
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
  // Heartbeat & Health Checking
  // ============================================================================
  
  /**
   * Start periodic heartbeat that detects dead sessions.
   * Checks if sessions have sent a ping within SERVER_TIMEOUT.
   */
  private startHeartbeat(): void {
    this.stopHeartbeat(); // Ensure no duplicate timers
    
    this.keepAliveTimer = setInterval(() => {
      const now = Date.now();
      const deadSessions: string[] = [];
      
      for (const [id, session] of this.sessions) {
        const timeSinceLastPing = now - session.lastPing.getTime();
        if (timeSinceLastPing > this.SERVER_TIMEOUT) {
          fileLog('WARN', 'WebSocketHub', `Session ${id} (${session.projectName}) timed out — no ping for ${timeSinceLastPing}ms`);
          deadSessions.push(id);
        }
      }
      
      // Clean up dead sessions
      for (const id of deadSessions) {
        const session = this.sessions.get(id);
        if (session) {
          try { session.socket.close(1001, 'Heartbeat timeout'); } catch { /* ignore */ }
          this.cleanupSession(id, session);
        }
      }
      
      // Also clean up stale rate limit entries
      this.cleanupRateLimits();
    }, this.KEEP_ALIVE_INTERVAL);
  }
  
  /**
   * Stop the heartbeat timer.
   */
  private stopHeartbeat(): void {
    if (this.keepAliveTimer) {
      clearInterval(this.keepAliveTimer);
      this.keepAliveTimer = null;
    }
  }
  
  /**
   * Handle incoming ping from Unity — respond with pong and update lastPing.
   */
  private handlePing(ws: WebSocket, message: PingMessage): void {
    // Find session by socket
    for (const session of this.sessions.values()) {
      if (session.socket === ws) {
        session.lastPing = new Date();
        try {
          ws.send(JSON.stringify({ type: 'pong' }));
        } catch { /* ignore send errors */ }
        fileLog('DEBUG', 'WebSocketHub', `Ping received from ${session.projectName}, sent pong`);
        return;
      }
    }
    // Ping from unregistered socket — still respond
    try {
      ws.send(JSON.stringify({ type: 'pong' }));
    } catch { /* ignore */ }
  }
  
  /**
   * Clean up a session and immediately reject all its pending commands.
   */
  private cleanupSession(sessionId: string, session: UnitySession): void {
    this.sessions.delete(sessionId);
    fileLog('INFO', 'WebSocketHub', `Session ${sessionId} cleaned up (${session.projectName})`);
    console.error(`[UnityVision] Unity disconnected: ${session.projectName}`);
    
    // Immediately reject all pending commands that belonged to this session
    const commandsToReject: string[] = [];
    for (const [cmdId, pending] of this.pendingCommands) {
      if (pending.sessionId === sessionId) {
        commandsToReject.push(cmdId);
      }
    }
    
    for (const cmdId of commandsToReject) {
      const pending = this.pendingCommands.get(cmdId);
      if (pending) {
        clearTimeout(pending.timeout);
        pending.reject(new Error(`Unity session disconnected (${session.projectName}). Command '${pending.method}' aborted.`));
        this.pendingCommands.delete(cmdId);
        fileLog('WARN', 'WebSocketHub', `Rejected pending command ${pending.method} (${cmdId}) — session disconnected`);
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
    // Circuit breaker check — fast-fail if too many consecutive failures
    if (Date.now() < this.circuitBreakerUntil) {
      const remainingMs = this.circuitBreakerUntil - Date.now();
      throw new Error(
        `Circuit breaker open — ${this.consecutiveFailures} consecutive command failures. ` +
        `Unity may be unresponsive. Auto-retrying in ${Math.ceil(remainingMs / 1000)}s. ` +
        `Please ensure Unity is focused and not compiling.`
      );
    }
    
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
        this.consecutiveFailures++;
        fileLog('ERROR', 'WebSocketHub', `Command ${method} (${commandId}) timed out after ${this.COMMAND_TIMEOUT}ms (failures: ${this.consecutiveFailures})`);
        
        // Trip circuit breaker if threshold reached
        if (this.consecutiveFailures >= this.CIRCUIT_BREAKER_THRESHOLD) {
          this.circuitBreakerUntil = Date.now() + this.CIRCUIT_BREAKER_COOLDOWN;
          fileLog('WARN', 'WebSocketHub', `Circuit breaker OPEN — ${this.consecutiveFailures} failures, cooldown ${this.CIRCUIT_BREAKER_COOLDOWN}ms`);
        }
        
        reject(new Error(`Command '${method}' timed out after ${this.COMMAND_TIMEOUT}ms`));
      }, this.COMMAND_TIMEOUT);
      
      const pending: PendingCommand = {
        id: commandId,
        method,
        sessionId: session.sessionId,
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
        this.consecutiveFailures++;
        fileLog('ERROR', 'WebSocketHub', `Failed to send command: ${(error as Error).message}`);
        reject(new Error(`Failed to send command: ${(error as Error).message}`));
      }
    });
  }
  
  private async resolveSession(projectHash?: string): Promise<UnitySession> {
    // Quick check — is a session already available?
    const existing = this.findSession(projectHash);
    if (existing) return existing;
    
    // No session yet — register a waiter and wait for one to connect
    fileLog('DEBUG', 'WebSocketHub', `No Unity session available, waiting up to ${this.RECONNECT_GRACE_PERIOD}ms...`);
    
    return new Promise<UnitySession>((resolve, reject) => {
      const waiter = { resolve, projectHash };
      this.sessionWaiters.push(waiter);
      
      // Timeout after grace period
      const timer = setTimeout(() => {
        const idx = this.sessionWaiters.indexOf(waiter);
        if (idx !== -1) {
          this.sessionWaiters.splice(idx, 1);
        }
        reject(new Error(
          'No Unity instance connected. Please ensure Unity is running with the UnityVision package installed ' +
          'and the WebSocket client is connected.'
        ));
      }, this.RECONNECT_GRACE_PERIOD);
      
      // Wrap resolve to clear the timeout
      const originalResolve = waiter.resolve;
      waiter.resolve = (session: UnitySession | PromiseLike<UnitySession>) => {
        clearTimeout(timer);
        originalResolve(session);
      };
    });
  }
  
  /**
   * Find an existing session matching the given projectHash (or auto-select if only one).
   */
  private findSession(projectHash?: string): UnitySession | null {
    if (projectHash) {
      for (const session of this.sessions.values()) {
        if (session.projectHash === projectHash) {
          return session;
        }
      }
      return null;
    }
    
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
    
    return null;
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
    const now = Date.now();
    return {
      running: this.isRunning,
      port: this.port,
      keepAliveIntervalMs: this.KEEP_ALIVE_INTERVAL,
      serverTimeoutMs: this.SERVER_TIMEOUT,
      commandTimeoutMs: this.COMMAND_TIMEOUT,
      reconnectGracePeriodMs: this.RECONNECT_GRACE_PERIOD,
      circuitBreaker: {
        consecutiveFailures: this.consecutiveFailures,
        threshold: this.CIRCUIT_BREAKER_THRESHOLD,
        cooldownMs: this.CIRCUIT_BREAKER_COOLDOWN,
        openUntilUnixMs: this.circuitBreakerUntil,
        isOpen: now < this.circuitBreakerUntil,
        remainingOpenMs: Math.max(0, this.circuitBreakerUntil - now),
      },
      sessions: Array.from(this.sessions.values()).map(s => ({
        sessionId: s.sessionId,
        projectName: s.projectName,
        projectHash: s.projectHash,
        unityVersion: s.unityVersion,
        clientName: s.clientName,
        platform: s.platform,
        connectedAt: s.connectedAt.toISOString(),
        lastPing: s.lastPing.toISOString(),
        lastPingAgeMs: now - s.lastPing.getTime(),
        customToolCount: s.customTools.size,
      })),
      pendingCommands: {
        count: this.pendingCommands.size,
        inFlight: Array.from(this.pendingCommands.values()).map((p) => ({
          id: p.id,
          method: p.method,
          sessionId: p.sessionId,
          ageMs: now - p.startTime,
        })),
      },
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

export async function startWebSocketHub(port: number = 6400): Promise<WebSocketPluginHub> {
  const hub = getWebSocketHub(port);
  await hub.start();
  return hub;
}

