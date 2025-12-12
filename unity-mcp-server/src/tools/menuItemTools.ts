// ============================================================================
// UnityVision MCP Server - Menu Item Tools
// Execute Unity menu items
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export interface ExecuteMenuItemInput {
  menuPath: string;
}

export interface ExecuteMenuItemOutput {
  success: boolean;
  menuPath?: string;
  error?: string;
}

export interface ListMenuItemsInput {
  filter?: string;
  maxResults?: number;
}

export interface MenuItemInfo {
  path: string;
  hasShortcut: boolean;
  shortcut?: string;
  priority: number;
}

export interface ListMenuItemsOutput {
  items: MenuItemInfo[];
  totalCount: number;
}

export const menuItemToolDefinitions = [
  {
    name: 'execute_menu_item',
    description: `Execute a Unity Editor menu item by its path. This gives access to hundreds of built-in Unity functions.

Common examples:
- "GameObject/Create Empty" - Create empty GameObject
- "GameObject/3D Object/Cube" - Create a cube
- "Edit/Play" - Enter play mode
- "File/Save" - Save the current scene
- "Window/General/Console" - Open console window

Use list_menu_items to discover available menu items.`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        menuPath: {
          type: 'string',
          description: 'Full path to the menu item (e.g., "GameObject/Create Empty")',
        },
      },
      required: ['menuPath'],
    },
  },
  {
    name: 'list_menu_items',
    description: 'List available Unity menu items. Use this to discover what menu items are available before executing them.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        filter: {
          type: 'string',
          description: 'Filter menu items by substring (e.g., "GameObject" or "Create")',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum number of results. Default is 200.',
          default: 200,
        },
      },
      required: [] as string[],
    },
  },
];

export async function executeMenuItem(
  params: ExecuteMenuItemInput
): Promise<ExecuteMenuItemOutput> {
  const client = getBridgeClient();
  return client.call<ExecuteMenuItemInput, ExecuteMenuItemOutput>(
    'execute_menu_item',
    params
  );
}

export async function listMenuItems(
  params: ListMenuItemsInput
): Promise<ListMenuItemsOutput> {
  const client = getBridgeClient();
  return client.call<ListMenuItemsInput, ListMenuItemsOutput>(
    'list_menu_items',
    {
      filter: params.filter ?? '',
      maxResults: params.maxResults ?? 200,
    }
  );
}
