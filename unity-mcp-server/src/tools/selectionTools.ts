// ============================================================================
// UnityVision MCP Server - Selection Tools
// Tools for editor selection sync
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const selectionToolDefinitions = [
  {
    name: 'get_editor_selection',
    description: `Get the current editor selection - what GameObjects and assets the user has selected.

**Use Cases:**
- "What do I have selected?" - shows current selection
- Understanding user context before making suggestions
- "Tell me about the selected object" - get details of selection

**Returns:**
- List of selected GameObjects with components, tags, layers
- List of selected assets with paths and types
- Active (primary) selection info`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'set_editor_selection',
    description: `Select GameObjects or assets in the Unity Editor and optionally frame them in the Scene View.

**Use Cases:**
- "Select the Player object" - selects and focuses on it
- "Show me where the MainCamera is" - selects and frames it
- Directing user attention to specific objects`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPaths: {
          type: 'array',
          items: { type: 'string' },
          description: 'Paths to GameObjects to select (e.g., ["Canvas/Button", "Player"])',
        },
        assetPaths: {
          type: 'array',
          items: { type: 'string' },
          description: 'Paths to assets to select (e.g., ["Assets/Materials/Red.mat"])',
        },
        frameInSceneView: {
          type: 'boolean',
          description: 'Frame the selection in Scene View. Default: true',
          default: true,
        },
        focusInHierarchy: {
          type: 'boolean',
          description: 'Focus the Hierarchy window on selection. Default: true',
          default: true,
        },
      },
      required: [] as string[],
    },
  },
];

export async function getEditorSelection(): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_editor_selection', {});
}

export async function setEditorSelection(params: {
  gameObjectPaths?: string[];
  assetPaths?: string[];
  frameInSceneView?: boolean;
  focusInHierarchy?: boolean;
}): Promise<any> {
  const client = getBridgeClient();
  return client.call('set_editor_selection', params);
}
