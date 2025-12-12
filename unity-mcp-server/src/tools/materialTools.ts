// ============================================================================
// UnityVision MCP Server - Material Tools
// Tools for material and shader inspection/modification
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const materialToolDefinitions = [
  {
    name: 'get_material_properties',
    description: `Get all properties of a material - colors, floats, textures, vectors.

**Use Cases:**
- "What color is this material?"
- "Show me the shader properties"
- Understanding material setup before modifications

**Returns:**
- All shader properties with current values
- Property types and ranges
- Shader keywords and render queue`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        materialPath: {
          type: 'string',
          description: 'Asset path to material (e.g., "Assets/Materials/Red.mat")',
        },
        gameObjectPath: {
          type: 'string',
          description: 'Alternative: path to GameObject with Renderer to get its material',
        },
        materialIndex: {
          type: 'number',
          description: 'Which material on the renderer (if multiple). Default: 0',
          default: 0,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'set_material_property',
    description: `Modify a material property - change colors, values, textures.

**Use Cases:**
- "Change the albedo color to red"
- "Set the metallic value to 0.8"
- "Make it more transparent"

**Supports:**
- Colors: "red", "#FF0000", "RGBA(1,0,0,1)"
- Floats/Ranges: "0.5"
- Vectors: "(1, 2, 3, 4)"
- Textures: asset path`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        materialPath: {
          type: 'string',
          description: 'Asset path to material',
        },
        gameObjectPath: {
          type: 'string',
          description: 'Alternative: path to GameObject with Renderer',
        },
        materialIndex: {
          type: 'number',
          description: 'Which material on the renderer. Default: 0',
          default: 0,
        },
        propertyName: {
          type: 'string',
          description: 'Shader property name (e.g., "_Color", "_MainTex", "_Metallic")',
        },
        value: {
          type: 'string',
          description: 'New value (color name, hex, RGBA, float, or texture path)',
        },
        createInstance: {
          type: 'boolean',
          description: 'Create material instance (for runtime changes). Default: false',
          default: false,
        },
      },
      required: ['propertyName', 'value'] as string[],
    },
  },
  {
    name: 'list_materials',
    description: `List all materials in the project with filtering options.

**Use Cases:**
- "What materials use the Standard shader?"
- "Find all materials named 'Metal'"
- Overview of project materials`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        folder: {
          type: 'string',
          description: 'Folder to search in. Default: "Assets"',
          default: 'Assets',
        },
        shaderFilter: {
          type: 'string',
          description: 'Filter by shader name (partial match)',
        },
        nameFilter: {
          type: 'string',
          description: 'Filter by material name (partial match)',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum results to return. Default: 100',
          default: 100,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'list_shaders',
    description: `List all available shaders in the project.

**Use Cases:**
- "What shaders are available?"
- Finding the right shader for a material`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
];

export async function getMaterialProperties(params: {
  materialPath?: string;
  gameObjectPath?: string;
  materialIndex?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_material_properties', params);
}

export async function setMaterialProperty(params: {
  materialPath?: string;
  gameObjectPath?: string;
  materialIndex?: number;
  propertyName: string;
  value: string;
  createInstance?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('set_material_property', params);
}

export async function listMaterials(params: {
  folder?: string;
  shaderFilter?: string;
  nameFilter?: string;
  maxResults?: number;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('list_materials', params);
}

export async function listShaders(): Promise<any> {
  const client = getBridgeClient();
  return client.call('list_shaders', {});
}
