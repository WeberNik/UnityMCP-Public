// ============================================================================
// UnityVision MCP Server - Scene Tools
// Tools for scene listing and hierarchy inspection
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  ListScenesInput,
  ListScenesOutput,
  GetSceneHierarchyInput,
  GetSceneHierarchyOutput,
} from '../types.js';

export const sceneToolDefinitions = [
  {
    name: 'list_scenes',
    description: 'List all scenes in the project. Optionally filter by name substring.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        filter: {
          type: 'string',
          description: 'Optional substring to filter scene names.',
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'get_scene_hierarchy',
    description: 'Get the GameObject hierarchy of a scene as a tree structure. Useful for understanding scene structure. Supports pagination via rootPath and maxObjects.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        sceneName: {
          type: 'string',
          description: 'Name of the scene to inspect. If not provided, uses the active scene.',
        },
        rootPath: {
          type: 'string',
          description: 'Start from a specific GameObject path instead of scene root. Useful for inspecting a subtree.',
        },
        maxDepth: {
          type: 'number',
          description: 'Maximum depth of the hierarchy tree. Default is 5.',
          default: 5,
        },
        includeComponents: {
          type: 'boolean',
          description: 'Whether to include component names on each GameObject. Default is false.',
          default: false,
        },
        nameFilter: {
          type: 'string',
          description: 'Optional filter to only include GameObjects whose names contain this substring.',
        },
        maxObjects: {
          type: 'number',
          description: 'Maximum number of objects to return. Default is 500. Use to prevent huge responses on large scenes.',
          default: 500,
        },
      },
      required: [] as string[],
    },
  },
];

export async function listScenes(
  params: ListScenesInput
): Promise<ListScenesOutput> {
  const client = getBridgeClient();
  return client.call<ListScenesInput, ListScenesOutput>(
    'list_scenes',
    { filter: params.filter ?? '' }
  );
}

export async function getSceneHierarchy(
  params: GetSceneHierarchyInput
): Promise<GetSceneHierarchyOutput> {
  const client = getBridgeClient();
  return client.call<GetSceneHierarchyInput, GetSceneHierarchyOutput>(
    'get_scene_hierarchy',
    {
      sceneName: params.sceneName ?? '',
      rootPath: params.rootPath ?? '',
      maxDepth: params.maxDepth ?? 5,
      includeComponents: params.includeComponents ?? false,
      nameFilter: params.nameFilter ?? '',
      maxObjects: params.maxObjects ?? 500,
    }
  );
}

// ============================================================================
// Extended Scene Operations (Phase 43)
// ============================================================================

export async function createScene(params: { path: string; template?: string }): Promise<unknown> {
  const client = getBridgeClient();
  return client.call('scene_create', params);
}

export async function loadScene(params: { path: string; additive?: boolean }): Promise<unknown> {
  const client = getBridgeClient();
  return client.call('scene_load', params);
}

export async function saveScene(params: { path?: string; saveAs?: string }): Promise<unknown> {
  const client = getBridgeClient();
  return client.call('scene_save', params);
}

export async function deleteScene(params: { path: string }): Promise<unknown> {
  const client = getBridgeClient();
  return client.call('scene_delete', params);
}
