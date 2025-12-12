// ============================================================================
// UnityVision MCP Server - Project Management Tools
// Tools for discovering, listing, and switching between Unity projects
// ============================================================================

import { z } from 'zod';
import { getProjectRegistry, ProjectInfo } from '../projectRegistry.js';

// ============================================================================
// Tool Definitions
// ============================================================================

export const listUnityProjectsTool = {
  name: 'list_unity_projects',
  description: `List all Unity projects with UnityVision installed that are currently running or recently active.
Returns project name, path, port, connection status, and Unity version.
Use this to discover available projects before switching.`,
  inputSchema: z.object({
    includeDisconnected: z.boolean()
      .optional()
      .default(false)
      .describe('Include projects that are registered but not currently reachable'),
  }),
};

export const switchProjectTool = {
  name: 'switch_project',
  description: `Switch the active Unity project target.
All subsequent tool calls will be routed to this project.
Use list_unity_projects first to see available projects.`,
  inputSchema: z.object({
    identifier: z.string()
      .describe('Project path or project name to switch to'),
  }),
};

export const getActiveProjectTool = {
  name: 'get_active_project',
  description: `Get information about the currently active Unity project.
Returns project name, path, port, connection status, and Unity version.
Returns null if no project is connected.`,
  inputSchema: z.object({}),
};

// ============================================================================
// Tool Handlers
// ============================================================================

export interface ListUnityProjectsParams {
  includeDisconnected?: boolean;
}

export interface ListUnityProjectsResult {
  projects: Array<{
    projectName: string;
    projectPath: string;
    port: number;
    status: 'connected' | 'disconnected' | 'unknown';
    unityVersion: string;
    isActive: boolean;
    lastSeen?: string;
    latencyMs?: number;
  }>;
  activeProject: string | null;
  message: string;
}

export async function handleListUnityProjects(
  params: ListUnityProjectsParams
): Promise<ListUnityProjectsResult> {
  const registry = getProjectRegistry();
  
  let projects: ProjectInfo[];
  if (params.includeDisconnected) {
    projects = registry.getProjects();
  } else {
    projects = registry.getConnectedProjects();
  }
  
  const activeProject = registry.getActiveProject();
  
  const result: ListUnityProjectsResult = {
    projects: projects.map(p => ({
      projectName: p.projectName,
      projectPath: p.projectPath,
      port: p.port,
      status: p.status,
      unityVersion: p.unityVersion,
      isActive: activeProject?.projectPath === p.projectPath,
      lastSeen: p.lastSeen,
      latencyMs: p.latencyMs,
    })),
    activeProject: activeProject?.projectPath || null,
    message: projects.length === 0
      ? 'No Unity projects found. Make sure Unity Editor is running with UnityVision package installed.'
      : `Found ${projects.length} Unity project(s)`,
  };
  
  return result;
}

export interface SwitchProjectParams {
  identifier: string;
}

export interface SwitchProjectResult {
  success: boolean;
  project: {
    projectName: string;
    projectPath: string;
    port: number;
    unityVersion: string;
  } | null;
  message: string;
}

export async function handleSwitchProject(
  params: SwitchProjectParams
): Promise<SwitchProjectResult> {
  const registry = getProjectRegistry();
  
  const project = await registry.setActiveProject(params.identifier);
  
  if (!project) {
    // Try to help the user
    const available = registry.getConnectedProjects();
    let message = `Could not switch to project "${params.identifier}". `;
    
    if (available.length === 0) {
      message += 'No Unity projects are currently connected.';
    } else {
      message += `Available projects: ${available.map(p => p.projectName).join(', ')}`;
    }
    
    return {
      success: false,
      project: null,
      message,
    };
  }
  
  return {
    success: true,
    project: {
      projectName: project.projectName,
      projectPath: project.projectPath,
      port: project.port,
      unityVersion: project.unityVersion,
    },
    message: `Switched to project "${project.projectName}" on port ${project.port}`,
  };
}

export interface GetActiveProjectResult {
  hasActiveProject: boolean;
  project: {
    projectName: string;
    projectPath: string;
    port: number;
    status: 'connected' | 'disconnected' | 'unknown';
    unityVersion: string;
    lastSeen?: string;
    latencyMs?: number;
  } | null;
  message: string;
}

export async function handleGetActiveProject(): Promise<GetActiveProjectResult> {
  const registry = getProjectRegistry();
  const project = registry.getActiveProject();
  
  if (!project) {
    const available = registry.getConnectedProjects();
    let message = 'No active Unity project. ';
    
    if (available.length === 0) {
      message += 'No Unity projects are currently connected. Make sure Unity Editor is running with UnityVision package installed.';
    } else if (available.length === 1) {
      message += `One project available: "${available[0].projectName}". It will be auto-selected on next tool call.`;
    } else {
      message += `${available.length} projects available. Use switch_project to select one.`;
    }
    
    return {
      hasActiveProject: false,
      project: null,
      message,
    };
  }
  
  return {
    hasActiveProject: true,
    project: {
      projectName: project.projectName,
      projectPath: project.projectPath,
      port: project.port,
      status: project.status,
      unityVersion: project.unityVersion,
      lastSeen: project.lastSeen,
      latencyMs: project.latencyMs,
    },
    message: `Active project: "${project.projectName}" (${project.status})`,
  };
}
