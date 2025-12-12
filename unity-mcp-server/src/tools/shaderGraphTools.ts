// ============================================================================
// UnityVision MCP Server - ShaderGraph Tools
// Tools for inspecting and creating ShaderGraph assets
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const shaderGraphToolDefinitions = [
  {
    name: 'get_shadergraph_info',
    description: `Get detailed information about a ShaderGraph asset.

**Returns:**
- Node list with types and positions
- Exposed properties
- Keywords
- Connection count

**Use Cases:**
- "What nodes are in this shader?"
- "What properties does this shader expose?"
- Analyzing shader complexity`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        assetPath: {
          type: 'string',
          description: "Path to the .shadergraph file (e.g., 'Assets/Shaders/MyShader.shadergraph')",
        },
      },
      required: ['assetPath'],
    },
  },
  {
    name: 'list_shadergraphs',
    description: `List all ShaderGraph assets in the project.

**Returns:**
- List of ShaderGraph paths
- Graph types (PBR, Unlit, etc.)

**Use Cases:**
- "Show me all shaders in the project"
- "Find ShaderGraphs in a specific folder"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        folder: {
          type: 'string',
          description: "Folder to search in (default: 'Assets')",
        },
        includePackages: {
          type: 'boolean',
          description: 'Include ShaderGraphs from packages (default: false)',
        },
      },
    },
  },
  {
    name: 'create_shadergraph',
    description: `Create a new ShaderGraph from a template.

**Templates:**
- PBR: Physically-based rendering (metallic/smoothness)
- Unlit: No lighting, just color/texture
- Sprite: For 2D sprites

**Use Cases:**
- "Create a new PBR shader"
- "Make an unlit shader for UI"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        path: {
          type: 'string',
          description: "Path where to create the ShaderGraph (e.g., 'Assets/Shaders/NewShader.shadergraph')",
        },
        template: {
          type: 'string',
          description: "Template type: 'PBR', 'Unlit', or 'Sprite'",
          enum: ['PBR', 'Unlit', 'Sprite'],
        },
        name: {
          type: 'string',
          description: 'Display name for the shader',
        },
      },
      required: ['path'],
    },
  },
  {
    name: 'list_shadergraph_node_types',
    description: `List all available ShaderGraph node types organized by category.

**Categories:**
- Input (Basic, Texture, Geometry, Scene)
- Math (Basic, Advanced, Trigonometry, Vector)
- UV manipulation
- Artistic (Adjustment, Blend, Normal)
- Procedural (Noise, Shapes)
- Utility

**Use Cases:**
- "What nodes can I use in ShaderGraph?"
- "Show me noise nodes"`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
    },
  },
];

// Tool Handlers
export async function handleGetShaderGraphInfo(params: { assetPath: string }) {
  const client = getBridgeClient();
  return await client.call('get_shadergraph_info', params);
}

export async function handleListShaderGraphs(params: { folder?: string; includePackages?: boolean }) {
  const client = getBridgeClient();
  return await client.call('list_shadergraphs', params);
}

export async function handleCreateShaderGraph(params: {
  path: string;
  template?: string;
  name?: string;
}) {
  const client = getBridgeClient();
  return await client.call('create_shadergraph', params);
}

export async function handleListShaderGraphNodeTypes() {
  const client = getBridgeClient();
  return await client.call('list_shadergraph_node_types', {});
}

// Handler map
export const shaderGraphToolHandlers: Record<string, (params: any) => Promise<any>> = {
  'get_shadergraph_info': handleGetShaderGraphInfo,
  'list_shadergraphs': handleListShaderGraphs,
  'create_shadergraph': handleCreateShaderGraph,
  'list_shadergraph_node_types': handleListShaderGraphNodeTypes,
};
