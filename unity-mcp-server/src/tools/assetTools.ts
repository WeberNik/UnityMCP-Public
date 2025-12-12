// ============================================================================
// UnityVision MCP Server - Asset Database Tools
// Asset search, creation, and management
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import { Vector3 } from '../types.js';

export interface SearchAssetsInput {
  filter?: string;
  type?: string;
  labels?: string[];
  folder?: string;
  maxResults?: number;
}

export interface AssetInfo {
  name: string;
  path: string;
  type: string;
  guid: string;
  sizeBytes: number;
  labels: string[];
}

export interface SearchAssetsOutput {
  assets: AssetInfo[];
  totalCount: number;
}

export interface CreateFolderInput {
  parentFolder: string;
  folderName: string;
}

export interface CreateFolderOutput {
  success: boolean;
  path?: string;
  guid?: string;
}

export interface MoveAssetInput {
  sourcePath: string;
  destinationPath: string;
}

export interface MoveAssetOutput {
  success: boolean;
  newPath?: string;
  error?: string;
}

export interface DeleteAssetInput {
  path: string;
  confirm?: boolean;
}

export interface DeleteAssetOutput {
  success: boolean;
  error?: string;
}

export interface CreatePrefabInput {
  gameObjectPath: string;
  savePath: string;
  overwrite?: boolean;
}

export interface CreatePrefabOutput {
  success: boolean;
  prefabPath?: string;
  guid?: string;
  error?: string;
}

export interface InstantiatePrefabInput {
  prefabPath: string;
  parentPath?: string;
  position?: Vector3;
  rotation?: Vector3;
}

export interface InstantiatePrefabOutput {
  success: boolean;
  instancePath?: string;
  instanceId?: string;
  error?: string;
}

export interface GetAssetInfoInput {
  path: string;
}

export interface GetAssetInfoOutput {
  success: boolean;
  asset?: AssetInfo;
  importSettings?: Record<string, unknown>;
  dependencies?: string[];
  error?: string;
}

export const assetToolDefinitions = [
  {
    name: 'search_assets',
    description: `Search the Unity Asset Database for assets by name, type, or label.

Examples:
- Search all textures: type="Texture2D"
- Search prefabs: type="Prefab"
- Search by name: filter="Player"
- Search in folder: folder="Assets/Prefabs"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        filter: {
          type: 'string',
          description: 'Search filter string (asset name)',
        },
        type: {
          type: 'string',
          description: 'Asset type filter (e.g., "Texture2D", "Material", "Prefab", "AudioClip", "Script")',
        },
        labels: {
          type: 'array',
          items: { type: 'string' },
          description: 'Filter by asset labels',
        },
        folder: {
          type: 'string',
          description: 'Folder to search in. Default is "Assets".',
          default: 'Assets',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results to return. Default is 100.',
          default: 100,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'create_folder',
    description: 'Create a new folder in the Asset Database.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        parentFolder: {
          type: 'string',
          description: 'Parent folder path (e.g., "Assets/Scripts")',
        },
        folderName: {
          type: 'string',
          description: 'Name of the new folder',
        },
      },
      required: ['parentFolder', 'folderName'],
    },
  },
  {
    name: 'move_asset',
    description: 'Move or rename an asset in the Asset Database.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        sourcePath: {
          type: 'string',
          description: 'Current path of the asset',
        },
        destinationPath: {
          type: 'string',
          description: 'New path for the asset',
        },
      },
      required: ['sourcePath', 'destinationPath'],
    },
  },
  {
    name: 'delete_asset',
    description: 'Delete an asset from the Asset Database. Requires confirmation.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: {
          type: 'string',
          description: 'Path to the asset to delete',
        },
        confirm: {
          type: 'boolean',
          description: 'Must be true to actually delete. Safety measure.',
          default: false,
        },
      },
      required: ['path'],
    },
  },
  {
    name: 'create_prefab',
    description: 'Create a prefab from an existing GameObject in the scene.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the GameObject in the scene hierarchy',
        },
        savePath: {
          type: 'string',
          description: 'Path to save the prefab (e.g., "Assets/Prefabs/Player.prefab")',
        },
        overwrite: {
          type: 'boolean',
          description: 'Overwrite if prefab already exists. Default is false.',
          default: false,
        },
      },
      required: ['gameObjectPath', 'savePath'],
    },
  },
  {
    name: 'instantiate_prefab',
    description: 'Instantiate a prefab into the current scene.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        prefabPath: {
          type: 'string',
          description: 'Path to the prefab asset',
        },
        parentPath: {
          type: 'string',
          description: 'Optional parent GameObject path',
        },
        position: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Position to instantiate at',
        },
        rotation: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Rotation (Euler angles)',
        },
      },
      required: ['prefabPath'],
    },
  },
  {
    name: 'get_asset_info',
    description: 'Get detailed information about an asset including import settings and dependencies.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: {
          type: 'string',
          description: 'Path to the asset',
        },
      },
      required: ['path'],
    },
  },
];

export async function searchAssets(params: SearchAssetsInput): Promise<SearchAssetsOutput> {
  const client = getBridgeClient();
  return client.call<SearchAssetsInput, SearchAssetsOutput>('search_assets', {
    filter: params.filter ?? '',
    type: params.type ?? '',
    labels: params.labels ?? [],
    folder: params.folder ?? 'Assets',
    maxResults: params.maxResults ?? 100,
  });
}

export async function createFolder(params: CreateFolderInput): Promise<CreateFolderOutput> {
  const client = getBridgeClient();
  return client.call<CreateFolderInput, CreateFolderOutput>('create_folder', params);
}

export async function moveAsset(params: MoveAssetInput): Promise<MoveAssetOutput> {
  const client = getBridgeClient();
  return client.call<MoveAssetInput, MoveAssetOutput>('move_asset', params);
}

export async function deleteAsset(params: DeleteAssetInput): Promise<DeleteAssetOutput> {
  const client = getBridgeClient();
  return client.call<DeleteAssetInput, DeleteAssetOutput>('delete_asset', {
    path: params.path,
    confirm: params.confirm ?? false,
  });
}

export async function createPrefab(params: CreatePrefabInput): Promise<CreatePrefabOutput> {
  const client = getBridgeClient();
  return client.call<CreatePrefabInput, CreatePrefabOutput>('create_prefab', {
    gameObjectPath: params.gameObjectPath,
    savePath: params.savePath,
    overwrite: params.overwrite ?? false,
  });
}

export async function instantiatePrefab(params: InstantiatePrefabInput): Promise<InstantiatePrefabOutput> {
  const client = getBridgeClient();
  return client.call<InstantiatePrefabInput, InstantiatePrefabOutput>('instantiate_prefab', {
    prefabPath: params.prefabPath,
    parentPath: params.parentPath ?? '',
    position: params.position,
    rotation: params.rotation,
  });
}

export async function getAssetInfo(params: GetAssetInfoInput): Promise<GetAssetInfoOutput> {
  const client = getBridgeClient();
  return client.call<GetAssetInfoInput, GetAssetInfoOutput>('get_asset_info', params);
}
