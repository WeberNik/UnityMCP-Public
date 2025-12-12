// ============================================================================
// UnityVision MCP Server - Prefab Tools
// Tools for prefab inspection and management
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const prefabToolDefinitions = [
  {
    name: 'get_prefab_overrides',
    description: `Get all overrides on a prefab instance - what differs from the prefab asset.

**Returns:**
- Property overrides with current vs prefab values
- Added/removed components
- Added/removed child GameObjects

**Use Cases:**
- "What's different on this prefab instance?"
- "Why doesn't this match the prefab?"
- Understanding prefab modifications`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the prefab instance in the scene',
        },
      },
      required: ['gameObjectPath'] as string[],
    },
  },
  {
    name: 'apply_prefab_overrides',
    description: `Apply prefab instance overrides back to the prefab asset.

**Use Cases:**
- "Apply these changes to the prefab"
- Syncing instance modifications to source`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the prefab instance',
        },
        applyAll: {
          type: 'boolean',
          description: 'Apply all overrides. Default: true',
          default: true,
        },
      },
      required: ['gameObjectPath'] as string[],
    },
  },
  {
    name: 'revert_prefab_overrides',
    description: `Revert prefab instance to match the prefab asset.

**Use Cases:**
- "Reset this to the original prefab"
- Undoing instance modifications`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the prefab instance',
        },
        applyAll: {
          type: 'boolean',
          description: 'Revert all overrides. Default: true',
          default: true,
        },
      },
      required: ['gameObjectPath'] as string[],
    },
  },
  {
    name: 'find_prefab_instances',
    description: `Find all instances of a prefab in the loaded scenes.

**Use Cases:**
- "Where is PlayerPrefab used?"
- "How many enemies are in the scene?"
- Finding all instances before modifying prefab`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        prefabPath: {
          type: 'string',
          description: 'Asset path to the prefab (e.g., "Assets/Prefabs/Player.prefab")',
        },
        includeNestedPrefabs: {
          type: 'boolean',
          description: 'Include nested prefab instances. Default: true',
          default: true,
        },
      },
      required: ['prefabPath'] as string[],
    },
  },
];

export async function getPrefabOverrides(params: {
  gameObjectPath: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_prefab_overrides', params);
}

export async function applyPrefabOverrides(params: {
  gameObjectPath: string;
  applyAll?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('apply_prefab_overrides', params);
}

export async function revertPrefabOverrides(params: {
  gameObjectPath: string;
  applyAll?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('revert_prefab_overrides', params);
}

export async function findPrefabInstances(params: {
  prefabPath: string;
  includeNestedPrefabs?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('find_prefab_instances', params);
}
