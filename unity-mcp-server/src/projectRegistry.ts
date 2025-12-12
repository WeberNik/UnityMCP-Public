// ============================================================================
// UnityVision MCP Server - Project Registry
// Discovers and manages connections to multiple Unity projects
// ============================================================================

import { EventEmitter } from 'events';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

/**
 * Registry entry for a Unity project (matches Unity-side ProjectEntry)
 */
export interface ProjectEntry {
  projectPath: string;
  projectName: string;
  pipeName?: string;  // Named pipe identifier (e.g., "unityvision-a1b2c3d4")
  port: number;       // Legacy HTTP port
  pid: number;
  unityVersion: string;
  lastSeen: string;
  isActive: boolean;
}

/**
 * Extended project info with connection status
 */
export interface ProjectInfo extends ProjectEntry {
  status: 'connected' | 'disconnected' | 'unknown';
  lastHealthCheck?: string;
  latencyMs?: number;
}

/**
 * Health check response from Unity bridge
 */
interface HealthResponse {
  status: string;
  pipeName?: string;  // Named pipe identifier
  port: number;
  projectName: string;
  projectPath: string;
  unityVersion: string;
  authRequired: boolean;
  isCompiling: boolean;
  requestCount: number;
  lastRequestTime?: string;
}

/**
 * Project Registry - discovers and tracks Unity projects
 * 
 * Events:
 * - 'projectConnected': (project: ProjectInfo) => void
 * - 'projectDisconnected': (project: ProjectInfo) => void
 * - 'projectsChanged': (projects: ProjectInfo[]) => void
 * - 'activeProjectChanged': (project: ProjectInfo | null) => void
 */
export class ProjectRegistry extends EventEmitter {
  private static readonly REGISTRY_FOLDER = '.unityvision';
  private static readonly REGISTRY_FILE = 'projects.json';
  private static readonly HEALTH_CHECK_INTERVAL = 5000; // 5 seconds
  private static readonly STALE_THRESHOLD = 30000; // 30 seconds
  private static readonly MAX_RECONNECT_ATTEMPTS = 5;
  private static readonly INITIAL_BACKOFF_MS = 1000;

  private registryPath: string;
  private projects: Map<string, ProjectInfo> = new Map();
  private activeProjectPath: string | null = null;
  private healthCheckTimer: NodeJS.Timeout | null = null;
  private fileWatcher: fs.FSWatcher | null = null;
  private reconnectAttempts: Map<string, number> = new Map();
  private reconnectTimers: Map<string, NodeJS.Timeout> = new Map();

  constructor() {
    super();
    
    // Determine registry path
    const homeDir = os.homedir();
    const registryFolder = path.join(homeDir, ProjectRegistry.REGISTRY_FOLDER);
    this.registryPath = path.join(registryFolder, ProjectRegistry.REGISTRY_FILE);
  }

  /**
   * Start the registry - load projects and begin health checking
   */
  async start(): Promise<void> {
    // Load initial projects
    await this.loadRegistry();
    
    // Watch for file changes
    this.watchRegistry();
    
    // Start health check polling
    this.startHealthChecks();
    
    // Auto-connect if only one project
    await this.autoConnect();
  }

  /**
   * Stop the registry
   */
  stop(): void {
    if (this.healthCheckTimer) {
      clearInterval(this.healthCheckTimer);
      this.healthCheckTimer = null;
    }
    
    if (this.fileWatcher) {
      this.fileWatcher.close();
      this.fileWatcher = null;
    }
    
    // Clear all reconnect timers
    for (const timer of this.reconnectTimers.values()) {
      clearTimeout(timer);
    }
    this.reconnectTimers.clear();
    this.reconnectAttempts.clear();
  }

  /**
   * Get all known projects
   */
  getProjects(): ProjectInfo[] {
    return Array.from(this.projects.values());
  }

  /**
   * Get only connected projects
   */
  getConnectedProjects(): ProjectInfo[] {
    return this.getProjects().filter(p => p.status === 'connected');
  }

  /**
   * Get the active project
   */
  getActiveProject(): ProjectInfo | null {
    if (!this.activeProjectPath) return null;
    return this.projects.get(this.activeProjectPath) || null;
  }

  /**
   * Get the port of the active project
   */
  getActivePort(): number | null {
    const active = this.getActiveProject();
    return active?.port || null;
  }

  /**
   * Set the active project by path or name
   */
  async setActiveProject(identifier: string): Promise<ProjectInfo | null> {
    // Find by path or name
    let project = this.projects.get(identifier);
    
    if (!project) {
      // Try to find by name
      for (const p of this.projects.values()) {
        if (p.projectName.toLowerCase() === identifier.toLowerCase()) {
          project = p;
          break;
        }
      }
    }
    
    if (!project) {
      return null;
    }
    
    // Verify it's reachable
    const isReachable = await this.checkProjectHealth(project);
    if (!isReachable) {
      return null;
    }
    
    this.activeProjectPath = project.projectPath;
    this.emit('activeProjectChanged', project);
    
    return project;
  }

  /**
   * Load projects from the registry file
   */
  private async loadRegistry(): Promise<void> {
    try {
      if (!fs.existsSync(this.registryPath)) {
        return;
      }
      
      const content = fs.readFileSync(this.registryPath, 'utf-8');
      const entries: ProjectEntry[] = JSON.parse(content);
      
      for (const entry of entries) {
        const existing = this.projects.get(entry.projectPath);
        
        this.projects.set(entry.projectPath, {
          ...entry,
          status: existing?.status || 'unknown',
          lastHealthCheck: existing?.lastHealthCheck,
          latencyMs: existing?.latencyMs,
        });
      }
      
      // Remove projects no longer in registry
      for (const path of this.projects.keys()) {
        if (!entries.find(e => e.projectPath === path)) {
          this.projects.delete(path);
        }
      }
      
      this.emit('projectsChanged', this.getProjects());
    } catch (error) {
      // Registry file may not exist yet or be invalid
      console.error('[ProjectRegistry] Failed to load registry:', error);
    }
  }

  /**
   * Watch the registry file for changes
   */
  private watchRegistry(): void {
    try {
      const dir = path.dirname(this.registryPath);
      
      // Ensure directory exists
      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
      }
      
      this.fileWatcher = fs.watch(dir, async (eventType, filename) => {
        if (filename === ProjectRegistry.REGISTRY_FILE) {
          await this.loadRegistry();
          await this.autoConnect();
        }
      });
    } catch (error) {
      console.error('[ProjectRegistry] Failed to watch registry:', error);
    }
  }

  /**
   * Start periodic health checks
   */
  private startHealthChecks(): void {
    this.healthCheckTimer = setInterval(async () => {
      await this.checkAllProjects();
    }, ProjectRegistry.HEALTH_CHECK_INTERVAL);
    
    // Run immediately
    this.checkAllProjects();
  }

  /**
   * Check health of all registered projects
   */
  private async checkAllProjects(): Promise<void> {
    const checks = Array.from(this.projects.values()).map(async (project) => {
      const wasConnected = project.status === 'connected';
      const isConnected = await this.checkProjectHealth(project);
      
      if (isConnected && !wasConnected) {
        // Reset reconnect attempts on successful connection
        this.reconnectAttempts.delete(project.projectPath);
        const timer = this.reconnectTimers.get(project.projectPath);
        if (timer) {
          clearTimeout(timer);
          this.reconnectTimers.delete(project.projectPath);
        }
        
        this.emit('projectConnected', project);
        console.error(`[ProjectRegistry] Project connected: ${project.projectName} on port ${project.port}`);
      } else if (!isConnected && wasConnected) {
        this.emit('projectDisconnected', project);
        console.error(`[ProjectRegistry] Project disconnected: ${project.projectName}`);
        
        // Start reconnect attempts with exponential backoff
        this.scheduleReconnect(project);
        
        // If active project disconnected, try to find another
        if (this.activeProjectPath === project.projectPath) {
          await this.autoConnect();
        }
      }
    });
    
    await Promise.all(checks);
  }

  /**
   * Schedule a reconnection attempt with exponential backoff
   */
  private scheduleReconnect(project: ProjectInfo): void {
    const attempts = this.reconnectAttempts.get(project.projectPath) || 0;
    
    if (attempts >= ProjectRegistry.MAX_RECONNECT_ATTEMPTS) {
      console.error(`[ProjectRegistry] Max reconnect attempts (${ProjectRegistry.MAX_RECONNECT_ATTEMPTS}) reached for ${project.projectName}`);
      return;
    }
    
    // Exponential backoff: 1s, 2s, 4s, 8s, 16s
    const backoffMs = ProjectRegistry.INITIAL_BACKOFF_MS * Math.pow(2, attempts);
    
    console.error(`[ProjectRegistry] Scheduling reconnect attempt ${attempts + 1}/${ProjectRegistry.MAX_RECONNECT_ATTEMPTS} for ${project.projectName} in ${backoffMs}ms`);
    
    // Clear any existing timer
    const existingTimer = this.reconnectTimers.get(project.projectPath);
    if (existingTimer) {
      clearTimeout(existingTimer);
    }
    
    const timer = setTimeout(async () => {
      this.reconnectTimers.delete(project.projectPath);
      
      const isConnected = await this.checkProjectHealth(project);
      
      if (isConnected) {
        this.reconnectAttempts.delete(project.projectPath);
        this.emit('projectConnected', project);
        console.error(`[ProjectRegistry] Reconnected to ${project.projectName}`);
      } else {
        this.reconnectAttempts.set(project.projectPath, attempts + 1);
        this.scheduleReconnect(project);
      }
    }, backoffMs);
    
    this.reconnectTimers.set(project.projectPath, timer);
    this.reconnectAttempts.set(project.projectPath, attempts + 1);
  }

  /**
   * Check health of a single project
   */
  private async checkProjectHealth(project: ProjectInfo): Promise<boolean> {
    const startTime = Date.now();
    
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 5000);
      
      const response = await fetch(`http://localhost:${project.port}/health`, {
        method: 'GET',
        signal: controller.signal,
      });
      
      clearTimeout(timeoutId);
      
      if (!response.ok) {
        project.status = 'disconnected';
        return false;
      }
      
      const data = await response.json() as HealthResponse;
      
      // Update project info from health response
      project.status = 'connected';
      project.lastHealthCheck = new Date().toISOString();
      project.latencyMs = Date.now() - startTime;
      project.projectName = data.projectName || project.projectName;
      project.unityVersion = data.unityVersion || project.unityVersion;
      
      return true;
    } catch {
      project.status = 'disconnected';
      project.lastHealthCheck = new Date().toISOString();
      return false;
    }
  }

  /**
   * Auto-connect to a project if conditions are met
   */
  private async autoConnect(): Promise<void> {
    // If already have an active connected project, keep it
    const active = this.getActiveProject();
    if (active && active.status === 'connected') {
      return;
    }
    
    // Get connected projects
    const connected = this.getConnectedProjects();
    
    if (connected.length === 0) {
      // No projects available
      this.activeProjectPath = null;
      this.emit('activeProjectChanged', null);
      return;
    }
    
    if (connected.length === 1) {
      // Only one project - auto-connect
      this.activeProjectPath = connected[0].projectPath;
      this.emit('activeProjectChanged', connected[0]);
      return;
    }
    
    // Multiple projects - keep current if still valid, otherwise pick first
    if (this.activeProjectPath) {
      const current = this.projects.get(this.activeProjectPath);
      if (current && current.status === 'connected') {
        return;
      }
    }
    
    // Pick the first connected project
    this.activeProjectPath = connected[0].projectPath;
    this.emit('activeProjectChanged', connected[0]);
  }

  /**
   * Scan ports for Unity projects (fallback if registry not available)
   */
  async scanPorts(startPort: number = 7890, endPort: number = 7899): Promise<ProjectInfo[]> {
    const found: ProjectInfo[] = [];
    
    const checks = [];
    for (let port = startPort; port <= endPort; port++) {
      checks.push(this.probePort(port));
    }
    
    const results = await Promise.all(checks);
    
    for (const result of results) {
      if (result) {
        found.push(result);
        
        // Add to registry if not already there
        if (!this.projects.has(result.projectPath)) {
          this.projects.set(result.projectPath, result);
        }
      }
    }
    
    return found;
  }

  /**
   * Probe a single port for a Unity project
   */
  private async probePort(port: number): Promise<ProjectInfo | null> {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 2000);
      
      const response = await fetch(`http://localhost:${port}/health`, {
        method: 'GET',
        signal: controller.signal,
      });
      
      clearTimeout(timeoutId);
      
      if (!response.ok) {
        return null;
      }
      
      const data = await response.json() as HealthResponse;
      
      return {
        projectPath: data.projectPath,
        projectName: data.projectName,
        port: data.port,
        pid: 0, // Unknown from health check
        unityVersion: data.unityVersion,
        lastSeen: new Date().toISOString(),
        isActive: true,
        status: 'connected',
        lastHealthCheck: new Date().toISOString(),
      };
    } catch {
      return null;
    }
  }
}

// Singleton instance
let registryInstance: ProjectRegistry | null = null;

/**
 * Get the singleton project registry instance
 */
export function getProjectRegistry(): ProjectRegistry {
  if (!registryInstance) {
    registryInstance = new ProjectRegistry();
  }
  return registryInstance;
}

/**
 * Initialize and start the project registry
 */
export async function initProjectRegistry(): Promise<ProjectRegistry> {
  const registry = getProjectRegistry();
  await registry.start();
  return registry;
}
