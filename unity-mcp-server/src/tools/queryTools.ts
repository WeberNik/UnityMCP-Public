// ============================================================================
// UnityVision MCP Server - Query Tools
// Tools for advanced scene queries
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const queryToolDefinitions = [
  {
    name: 'find_objects_with_component',
    description: `Find all GameObjects that have a specific component.

**Use Cases:**
- "Find all objects with Rigidbody"
- "Which objects have the Enemy script?"
- "List all UI buttons in the scene"

**Filters:**
- Component type (partial match)
- Tag, Layer
- Active state
- Name contains`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        componentType: {
          type: 'string',
          description: 'Component type to search for (e.g., "Rigidbody", "Button")',
        },
        tag: {
          type: 'string',
          description: 'Filter by tag (exact match)',
        },
        layer: {
          type: 'string',
          description: 'Filter by layer name',
        },
        activeOnly: {
          type: 'boolean',
          description: 'Only return active objects',
        },
        nameContains: {
          type: 'string',
          description: 'Filter by name (partial match)',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results. Default: 100',
          default: 100,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'find_missing_references',
    description: `Find all missing/broken references in the scene.

**Detects:**
- Missing script components
- Null serialized object references
- Broken prefab connections

**Use Cases:**
- "Are there any broken references?"
- Debugging "MissingReferenceException"
- Scene cleanup before build`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'analyze_layers',
    description: `Analyze layer and tag usage in the scene.

**Returns:**
- All layers with object counts
- All tags with object counts
- Total object count

**Use Cases:**
- "What layers are being used?"
- Debugging physics layer issues
- Understanding scene organization`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'find_objects_in_radius',
    description: `Find all objects within a radius of a point.

**Use Cases:**
- "What objects are near the player?"
- Spatial debugging
- Finding nearby interactables

**Note:** Uses both physics (colliders) and transform positions.`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        center: {
          type: 'array',
          items: { type: 'number' },
          description: 'Center point [x, y, z]',
        },
        radius: {
          type: 'number',
          description: 'Search radius in units',
        },
        layerMask: {
          type: 'array',
          items: { type: 'string' },
          description: 'Layer names to include (optional)',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results. Default: 50',
          default: 50,
        },
      },
      required: ['center', 'radius'] as string[],
    },
  },
];

export async function findObjectsWithComponent(params: {
  componentType?: string;
  tag?: string;
  layer?: string;
  activeOnly?: boolean;
  nameContains?: string;
  maxResults?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('find_objects_with_component', params);
}

export async function findMissingReferences(): Promise<any> {
  const client = getBridgeClient();
  return client.call('find_missing_references', {});
}

export async function analyzeLayers(): Promise<any> {
  const client = getBridgeClient();
  return client.call('analyze_layers', {});
}

export async function findObjectsInRadius(params: {
  center: number[];
  radius: number;
  layerMask?: string[];
  maxResults?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('find_objects_in_radius', params);
}
