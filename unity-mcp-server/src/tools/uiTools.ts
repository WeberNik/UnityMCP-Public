// ============================================================================
// UnityVision MCP Server - UI Tools
// Tools for UI layout inspection
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  DumpUILayoutInput,
  DumpUILayoutOutput,
} from '../types.js';

export const uiToolDefinitions = [
  {
    name: 'dump_ui_layout',
    description: 'Dump the UI layout hierarchy starting from a Canvas or RectTransform. Returns structured data including anchors, positions, sizes, and component info.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        rootCanvasPath: {
          type: 'string',
          description: 'Path to the root Canvas or RectTransform to dump (e.g., "UI_Root/Canvas_MainMenu").',
        },
        maxDepth: {
          type: 'number',
          description: 'Maximum depth of the hierarchy to traverse. Default is 6.',
          default: 6,
        },
        includeInactive: {
          type: 'boolean',
          description: 'Whether to include inactive UI elements. Default is true.',
          default: true,
        },
      },
      required: ['rootCanvasPath'],
    },
  },
];

export async function dumpUILayout(
  params: DumpUILayoutInput
): Promise<DumpUILayoutOutput> {
  const client = getBridgeClient();
  return client.call<DumpUILayoutInput, DumpUILayoutOutput>(
    'dump_ui_layout',
    {
      rootCanvasPath: params.rootCanvasPath,
      maxDepth: params.maxDepth ?? 6,
      includeInactive: params.includeInactive ?? true,
    }
  );
}
