// ============================================================================
// UnityVision MCP Server - Tool Registry
// Central registry of all available tools
// Uses consolidated tools to reduce tool count from 72 to 24
// ============================================================================

import { UnityBridgeError } from '../types.js';
import {
  consolidatedToolDefinitions,
  consolidatedToolHandlers,
} from './consolidatedTools.js';

// All tool definitions for MCP registration (consolidated)
export const allToolDefinitions = consolidatedToolDefinitions;

// Tool handler type
type ToolHandler = (params: Record<string, unknown>) => Promise<unknown>;

// Map of tool names to their handlers (consolidated)
const toolHandlers: Record<string, ToolHandler> = consolidatedToolHandlers;

/**
 * Execute a tool by name with the given parameters
 * 
 * IMPORTANT: This function never throws errors for Unity connection issues.
 * Instead, it returns a graceful response indicating the connection status.
 * This ensures the MCP server always appears healthy to Windsurf/Claude,
 * even when Unity is not connected.
 */
export async function executeTool(
  toolName: string,
  params: Record<string, unknown>
): Promise<{ content: Array<{ type: 'text'; text: string }> }> {
  const handler = toolHandlers[toolName];

  if (!handler) {
    throw new Error(`Unknown tool: ${toolName}`);
  }

  try {
    const result = await handler(params);
    return {
      content: [
        {
          type: 'text',
          text: JSON.stringify(result, null, 2),
        },
      ],
    };
  } catch (error) {
    // Handle Unity connection errors gracefully - return as content, not as thrown error
    // This prevents Windsurf from showing the MCP as "Error" state
    if (error instanceof UnityBridgeError) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              status: 'waiting_for_unity',
              error: {
                code: error.code,
                message: error.message,
                details: error.details,
              },
              help: 'Unity Editor is not connected. Please open Unity with the UnityVision package installed.',
              next_steps: [
                '1. Open Unity Editor',
                '2. Ensure UnityVision package is installed (Window > Package Manager)',
                '3. Check Window > UnityVision > Bridge Status for connection status',
                '4. The connection will be established automatically when Unity starts'
              ]
            }, null, 2),
          },
        ],
      };
    }

    // For other errors (like "No Unity instance connected"), also return gracefully
    const errorMessage = error instanceof Error ? error.message : String(error);
    if (errorMessage.includes('No Unity instance connected') || 
        errorMessage.includes('Unity') ||
        errorMessage.includes('WebSocket')) {
      return {
        content: [
          {
            type: 'text',
            text: JSON.stringify({
              status: 'waiting_for_unity',
              message: 'Unity Editor is not connected yet.',
              error: errorMessage,
              help: 'The MCP server is running and ready. Unity will connect automatically when opened.',
              next_steps: [
                '1. Open Unity Editor with a project that has UnityVision package installed',
                '2. Check Window > UnityVision > Bridge Status in Unity',
                '3. Once connected, all tools will work automatically'
              ]
            }, null, 2),
          },
        ],
      };
    }

    // Re-throw unexpected errors
    throw error;
  }
}
