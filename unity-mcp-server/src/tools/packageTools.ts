// ============================================================================
// UnityVision MCP Server - Package Manager Tools
// Unity Package Manager operations (list, add, remove)
// ============================================================================

import { Tool } from '@modelcontextprotocol/sdk/types.js';
import { getBridgeClient } from '../unityBridgeClient.js';
import { fileLog } from '../fileLogger.js';

// ============================================================================
// Tool Definition
// ============================================================================

export const packageToolDefinition: Tool = {
  name: 'unity_package',
  description: 'Manage Unity packages. Actions: list (installed packages), add (install package), remove (uninstall package)',
  inputSchema: {
    type: 'object',
    properties: {
      action: {
        type: 'string',
        enum: ['list', 'add', 'remove'],
        description: 'The action to perform',
      },
      packageName: {
        type: 'string',
        description: 'For add/remove: Package name (e.g., "com.unity.textmeshpro")',
      },
      version: {
        type: 'string',
        description: 'For add: Specific version to install (optional)',
      },
      gitUrl: {
        type: 'string',
        description: 'For add: Git URL for package (alternative to packageName)',
      },
      includeBuiltIn: {
        type: 'boolean',
        description: 'For list: Include built-in packages',
      },
    },
    required: ['action'],
  },
};

// ============================================================================
// Tool Execution
// ============================================================================

export async function executePackageTool(args: Record<string, unknown>): Promise<{
  content: Array<{ type: string; text: string }>;
  isError?: boolean;
}> {
  const action = args.action as string;

  fileLog('INFO', 'PackageTools', `Executing unity_package: ${action}`);

  const client = getBridgeClient();

  try {
    const result = await client.call<Record<string, unknown>, unknown>(
      'unity_package',
      {
        action,
        packageName: args.packageName,
        version: args.version,
        gitUrl: args.gitUrl,
        includeBuiltIn: args.includeBuiltIn,
      }
    );

    return {
      content: [
        {
          type: 'text',
          text: JSON.stringify(result, null, 2),
        },
      ],
    };
  } catch (error) {
    fileLog('ERROR', 'PackageTools', `unity_package failed: ${(error as Error).message}`);
    return {
      content: [
        {
          type: 'text',
          text: `Error: ${(error as Error).message}`,
        },
      ],
      isError: true,
    };
  }
}
