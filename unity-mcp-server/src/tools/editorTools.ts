// ============================================================================
// UnityVision MCP Server - Editor Tools
// Tools for editor state and play mode control
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  GetEditorStateInput,
  GetEditorStateOutput,
  SetPlayModeInput,
  SetPlayModeOutput,
  GetActiveContextInput,
  GetActiveContextOutput,
} from '../types.js';

export const editorToolDefinitions = [
  {
    name: 'get_editor_state',
    description: 'Get the current state of the Unity Editor including version, project path, play mode status, and loaded scenes.',
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'set_play_mode',
    description: 'Control Unity Editor play mode. Start, pause, or stop the game.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        mode: {
          type: 'string',
          enum: ['play', 'pause', 'stop'],
          description: 'The play mode to set: "play" to start, "pause" to pause, "stop" to stop.',
        },
      },
      required: ['mode'],
    },
  },
  {
    name: 'get_active_context',
    description: 'Get a quick snapshot of the current Unity Editor context: selected objects, recent console errors, and play mode state. Use this at the start of a task to understand the current state.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        maxConsoleErrors: {
          type: 'number',
          description: 'Maximum number of recent console errors to include. Default is 5.',
          default: 5,
        },
        includeSelection: {
          type: 'boolean',
          description: 'Include currently selected GameObjects. Default is true.',
          default: true,
        },
        includePlayModeState: {
          type: 'boolean',
          description: 'Include play mode state and compilation status. Default is true.',
          default: true,
        },
      },
      required: [] as string[],
    },
  },
];

export async function getEditorState(
  _params: GetEditorStateInput
): Promise<GetEditorStateOutput> {
  const client = getBridgeClient();
  return client.call<GetEditorStateInput, GetEditorStateOutput>(
    'get_editor_state',
    {}
  );
}

export async function setPlayMode(
  params: SetPlayModeInput
): Promise<SetPlayModeOutput> {
  const client = getBridgeClient();
  return client.call<SetPlayModeInput, SetPlayModeOutput>(
    'set_play_mode',
    params
  );
}

export async function getActiveContext(
  params: GetActiveContextInput
): Promise<GetActiveContextOutput> {
  const client = getBridgeClient();
  return client.call<GetActiveContextInput, GetActiveContextOutput>(
    'get_active_context',
    {
      maxConsoleErrors: params.maxConsoleErrors ?? 5,
      includeSelection: params.includeSelection ?? true,
      includePlayModeState: params.includePlayModeState ?? true,
    }
  );
}

// ============================================================================
// Extended Editor Operations (Phase 44)
// ============================================================================

export async function recompileScripts(): Promise<unknown> {
  const client = getBridgeClient();
  return client.call('editor_recompile', {});
}

export async function refreshAssets(): Promise<unknown> {
  const client = getBridgeClient();
  return client.call('editor_refresh', {});
}
