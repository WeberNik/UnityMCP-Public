// ============================================================================
// UnityVision MCP Server - GameObject Tools
// Tools for creating, modifying, and deleting GameObjects
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  CreateGameObjectInput,
  CreateGameObjectOutput,
  ModifyGameObjectInput,
  ModifyGameObjectOutput,
  DeleteGameObjectInput,
  DeleteGameObjectOutput,
} from '../types.js';

export const gameObjectToolDefinitions = [
  {
    name: 'create_game_object',
    description: 'Create a new GameObject in the scene with optional components and transform. Supports dry-run mode to preview changes.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        sceneName: {
          type: 'string',
          description: 'Name of the scene to create the GameObject in. Uses active scene if not specified.',
        },
        parentPath: {
          type: 'string',
          description: 'Path to the parent GameObject (e.g., "UI_Root/Canvas"). Creates at root if not specified.',
        },
        name: {
          type: 'string',
          description: 'Name of the new GameObject.',
        },
        components: {
          type: 'array',
          items: {
            type: 'object',
            properties: {
              type: {
                type: 'string',
                description: 'Full type name of the component (e.g., "UnityEngine.UI.Image").',
              },
              properties: {
                type: 'object',
                description: 'Optional properties to set on the component.',
              },
            },
            required: ['type'],
          },
          description: 'Components to add to the GameObject.',
        },
        position: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Local position of the GameObject.',
        },
        rotation: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Local rotation (Euler angles) of the GameObject.',
        },
        scale: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Local scale of the GameObject.',
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns a plan of what would be created without actually creating it.',
          default: false,
        },
      },
      required: ['name'],
    },
  },
  {
    name: 'modify_game_object',
    description: 'Modify an existing GameObject. Change name, parent, transform, or active state. Supports dry-run mode.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: {
          type: 'string',
          description: 'Path to the GameObject to modify (e.g., "UI_Root/Canvas/Button").',
        },
        newName: {
          type: 'string',
          description: 'New name for the GameObject.',
        },
        parentPath: {
          type: ['string', 'null'],
          description: 'New parent path. Use null to move to scene root.',
        },
        transform: {
          type: 'object',
          properties: {
            position: {
              type: 'object',
              properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
            },
            rotation: {
              type: 'object',
              properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
            },
            scale: {
              type: 'object',
              properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
            },
          },
          description: 'Transform changes to apply.',
        },
        active: {
          type: 'boolean',
          description: 'Set the active state of the GameObject.',
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns a plan of what would be modified without actually modifying.',
          default: false,
        },
      },
      required: ['path'],
    },
  },
  {
    name: 'delete_game_object',
    description: 'Delete a GameObject from the scene. Requires confirmation. Supports dry-run mode to see what would be deleted.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: {
          type: 'string',
          description: 'Path to the GameObject to delete.',
        },
        confirm: {
          type: 'boolean',
          description: 'Must be true to actually delete. Safety measure for destructive operations.',
          default: false,
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns info about what would be deleted without actually deleting.',
          default: false,
        },
      },
      required: ['path'],
    },
  },
];

export async function createGameObject(
  params: CreateGameObjectInput
): Promise<CreateGameObjectOutput> {
  const client = getBridgeClient();
  return client.call<CreateGameObjectInput, CreateGameObjectOutput>(
    'create_game_object',
    {
      sceneName: params.sceneName ?? '',
      parentPath: params.parentPath ?? '',
      name: params.name,
      components: params.components ?? [],
      position: params.position ?? { x: 0, y: 0, z: 0 },
      rotation: params.rotation ?? { x: 0, y: 0, z: 0 },
      scale: params.scale ?? { x: 1, y: 1, z: 1 },
      dryRun: params.dryRun ?? false,
    }
  );
}

export async function modifyGameObject(
  params: ModifyGameObjectInput
): Promise<ModifyGameObjectOutput> {
  const client = getBridgeClient();
  return client.call<ModifyGameObjectInput, ModifyGameObjectOutput>(
    'modify_game_object',
    params
  );
}

export async function deleteGameObject(
  params: DeleteGameObjectInput
): Promise<DeleteGameObjectOutput> {
  const client = getBridgeClient();
  return client.call<DeleteGameObjectInput, DeleteGameObjectOutput>(
    'delete_game_object',
    {
      path: params.path,
      confirm: params.confirm ?? false,
      dryRun: params.dryRun ?? false,
    }
  );
}
