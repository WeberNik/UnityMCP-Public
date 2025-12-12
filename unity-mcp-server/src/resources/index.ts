// ============================================================================
// UnityVision MCP Server - Resources Index
// Exposes Unity data as MCP Resources for LLM context
// ============================================================================

import { Resource } from '@modelcontextprotocol/sdk/types.js';
import { getBridgeClient } from '../unityBridgeClient.js';
import { fileLog } from '../fileLogger.js';

// ============================================================================
// Resource Definitions
// ============================================================================

export interface UnityResource {
  uri: string;
  name: string;
  description: string;
  mimeType: string;
}

export const allResourceDefinitions: UnityResource[] = [
  {
    uri: 'unity://hierarchy',
    name: 'Scene Hierarchy',
    description: 'Current scene hierarchy tree with all GameObjects',
    mimeType: 'application/json',
  },
  {
    uri: 'unity://selection',
    name: 'Editor Selection',
    description: 'Currently selected GameObjects and assets in the Unity Editor',
    mimeType: 'application/json',
  },
  {
    uri: 'unity://logs',
    name: 'Console Logs',
    description: 'Recent console log messages (info, warnings, errors)',
    mimeType: 'application/json',
  },
  {
    uri: 'unity://project-info',
    name: 'Project Information',
    description: 'Unity project metadata including name, version, platform, and packages',
    mimeType: 'application/json',
  },
  {
    uri: 'unity://editor-state',
    name: 'Editor State',
    description: 'Current Unity Editor state including play mode, compilation status',
    mimeType: 'application/json',
  },
];

// ============================================================================
// Resource Fetching
// ============================================================================

export async function fetchResource(uri: string): Promise<string> {
  const client = getBridgeClient();
  
  fileLog('INFO', 'Resources', `Fetching resource: ${uri}`);
  
  try {
    switch (uri) {
      case 'unity://hierarchy':
        return await fetchHierarchy(client);
      case 'unity://selection':
        return await fetchSelection(client);
      case 'unity://logs':
        return await fetchLogs(client);
      case 'unity://project-info':
        return await fetchProjectInfo(client);
      case 'unity://editor-state':
        return await fetchEditorState(client);
      default:
        throw new Error(`Unknown resource URI: ${uri}`);
    }
  } catch (error) {
    fileLog('ERROR', 'Resources', `Failed to fetch ${uri}: ${(error as Error).message}`);
    throw error;
  }
}

// ============================================================================
// Individual Resource Fetchers
// ============================================================================

async function fetchHierarchy(client: ReturnType<typeof getBridgeClient>): Promise<string> {
  const result = await client.call<{ sceneName?: string }, unknown>(
    'unity_scene',
    { action: 'hierarchy' } as any
  );
  return JSON.stringify(result, null, 2);
}

async function fetchSelection(client: ReturnType<typeof getBridgeClient>): Promise<string> {
  const result = await client.call<Record<string, unknown>, unknown>(
    'unity_selection',
    { action: 'get' } as any
  );
  return JSON.stringify(result, null, 2);
}

async function fetchLogs(client: ReturnType<typeof getBridgeClient>): Promise<string> {
  const result = await client.call<{ count?: number }, unknown>(
    'unity_console',
    { action: 'get_logs', count: 100 } as any
  );
  return JSON.stringify(result, null, 2);
}

async function fetchProjectInfo(client: ReturnType<typeof getBridgeClient>): Promise<string> {
  const result = await client.call<Record<string, unknown>, unknown>(
    'unity_project',
    { action: 'get_active' } as any
  );
  return JSON.stringify(result, null, 2);
}

async function fetchEditorState(client: ReturnType<typeof getBridgeClient>): Promise<string> {
  const result = await client.call<Record<string, unknown>, unknown>(
    'unity_editor',
    { action: 'get_state' } as any
  );
  return JSON.stringify(result, null, 2);
}

// ============================================================================
// Convert to MCP Resource Format
// ============================================================================

export function getResourceList(): Resource[] {
  return allResourceDefinitions.map(r => ({
    uri: r.uri,
    name: r.name,
    description: r.description,
    mimeType: r.mimeType,
  }));
}
