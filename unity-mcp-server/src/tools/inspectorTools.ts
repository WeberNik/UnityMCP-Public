// ============================================================================
// UnityVision MCP Server - Inspector Tools
// Tools for reading/writing component properties
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const inspectorToolDefinitions = [
  {
    name: 'get_component_properties',
    description: `Read all serialized properties of a component - see what the Inspector shows.

**Use Cases:**
- "What are the Rigidbody settings on the Player?"
- "Show me the current health value"
- Debugging runtime values during Play mode

**Returns:**
- All serialized fields with names, types, and current values
- Property metadata (tooltips, ranges)
- Works in both Edit and Play mode`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the GameObject (e.g., "Player" or "Canvas/Button")',
        },
        componentType: {
          type: 'string',
          description: 'Component type name (e.g., "Rigidbody", "PlayerController")',
        },
        includePrivate: {
          type: 'boolean',
          description: 'Include private serialized fields. Default: false',
          default: false,
        },
      },
      required: ['gameObjectPath', 'componentType'] as string[],
    },
  },
  {
    name: 'set_component_property',
    description: `Modify a component property value - like editing in the Inspector.

**Use Cases:**
- "Set the player speed to 10"
- "Change the button color to red"
- "Disable gravity on the Rigidbody"

**Supports:**
- Primitives (int, float, bool, string)
- Vectors, Colors, Quaternions
- Enums (by name or index)
- Undo/redo integration`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the GameObject',
        },
        componentType: {
          type: 'string',
          description: 'Component type name',
        },
        propertyName: {
          type: 'string',
          description: 'Property name (display name or field name)',
        },
        value: {
          type: 'string',
          description: 'New value as string (e.g., "10", "true", "(1, 2, 3)", "RGBA(1,0,0,1)")',
        },
        recordUndo: {
          type: 'boolean',
          description: 'Record for undo. Default: true',
          default: true,
        },
      },
      required: ['gameObjectPath', 'componentType', 'propertyName', 'value'] as string[],
    },
  },
  {
    name: 'compare_components',
    description: `Compare component values between two GameObjects to find differences.

**Use Cases:**
- "Why does this enemy behave differently?"
- "What's different between these two prefab instances?"
- Debugging inconsistent behavior`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath1: {
          type: 'string',
          description: 'Path to first GameObject',
        },
        gameObjectPath2: {
          type: 'string',
          description: 'Path to second GameObject',
        },
        componentType: {
          type: 'string',
          description: 'Component type to compare',
        },
      },
      required: ['gameObjectPath1', 'gameObjectPath2', 'componentType'] as string[],
    },
  },
];

export async function getComponentProperties(params: {
  gameObjectPath: string;
  componentType: string;
  includePrivate?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_component_properties', params);
}

export async function setComponentProperty(params: {
  gameObjectPath: string;
  componentType: string;
  propertyName: string;
  value: string;
  recordUndo?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('set_component_property', params);
}

export async function compareComponents(params: {
  gameObjectPath1: string;
  gameObjectPath2: string;
  componentType: string;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('compare_components', params);
}
