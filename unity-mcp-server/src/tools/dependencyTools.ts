// ============================================================================
// UnityVision MCP Server - Dependency Tools
// Tools for asset dependency analysis
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const dependencyToolDefinitions = [
  {
    name: 'find_asset_references',
    description: `Find all assets that reference/use a given asset.

**Use Cases:**
- "What uses this texture?"
- "What would break if I delete this script?"
- Understanding asset dependencies before changes

**Searches in:**
- Scenes
- Prefabs
- Materials
- ScriptableObjects`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        assetPath: {
          type: 'string',
          description: 'Path to the asset (e.g., "Assets/Textures/Player.png")',
        },
        searchInScenes: {
          type: 'boolean',
          description: 'Search in scene files. Default: true',
          default: true,
        },
        searchInPrefabs: {
          type: 'boolean',
          description: 'Search in prefabs. Default: true',
          default: true,
        },
        searchInMaterials: {
          type: 'boolean',
          description: 'Search in materials. Default: true',
          default: true,
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results. Default: 100',
          default: 100,
        },
      },
      required: ['assetPath'] as string[],
    },
  },
  {
    name: 'get_asset_dependencies',
    description: `Get all assets that a given asset depends on.

**Use Cases:**
- "What does this prefab need?"
- "What textures does this material use?"
- Understanding what an asset requires`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        assetPath: {
          type: 'string',
          description: 'Path to the asset',
        },
        recursive: {
          type: 'boolean',
          description: 'Include dependencies of dependencies. Default: false',
          default: false,
        },
        maxDepth: {
          type: 'number',
          description: 'Maximum recursion depth. Default: 3',
          default: 3,
        },
      },
      required: ['assetPath'] as string[],
    },
  },
  {
    name: 'find_unused_assets',
    description: `Find assets that are not referenced by any scene or prefab.

**Use Cases:**
- "What assets can I safely delete?"
- Project cleanup
- Reducing build size

**Note:** This can take 10-30 seconds on large projects.

**Excludes by default:**
- Editor/ folders
- Plugins/
- Resources/ (loaded at runtime)
- StreamingAssets/`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        folder: {
          type: 'string',
          description: 'Folder to scan. Default: "Assets"',
          default: 'Assets',
        },
        excludePatterns: {
          type: 'array',
          items: { type: 'string' },
          description: 'Patterns to exclude (e.g., ["Editor/", "Test/"])',
        },
        includeExtensions: {
          type: 'array',
          items: { type: 'string' },
          description: 'File extensions to check (e.g., [".png", ".mat"])',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results. Default: 200',
          default: 200,
        },
      },
      required: [] as string[],
    },
  },
];

export async function findAssetReferences(params: {
  assetPath: string;
  searchInScenes?: boolean;
  searchInPrefabs?: boolean;
  searchInMaterials?: boolean;
  maxResults?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('find_asset_references', params);
}

export async function getAssetDependencies(params: {
  assetPath: string;
  recursive?: boolean;
  maxDepth?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_asset_dependencies', params);
}

export async function findUnusedAssets(params: {
  folder?: string;
  excludePatterns?: string[];
  includeExtensions?: string[];
  maxResults?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('find_unused_assets', params);
}
