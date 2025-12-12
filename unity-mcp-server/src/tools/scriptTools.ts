// ============================================================================
// UnityVision MCP Server - Script Management Tools
// CRUD operations for C# scripts in Unity projects
// ============================================================================

import { Tool } from '@modelcontextprotocol/sdk/types.js';
import { getBridgeClient } from '../unityBridgeClient.js';
import { fileLog } from '../fileLogger.js';

// ============================================================================
// Tool Definition
// ============================================================================

export const scriptToolDefinition: Tool = {
  name: 'unity_script',
  description: 'Manage C# scripts in the Unity project. Actions: create, read, update, delete, validate, get_sha',
  inputSchema: {
    type: 'object',
    properties: {
      action: {
        type: 'string',
        enum: ['create', 'read', 'update', 'delete', 'validate', 'get_sha'],
        description: 'The action to perform',
      },
      path: {
        type: 'string',
        description: 'Path to the script relative to Assets/ (e.g., "Scripts/PlayerController.cs")',
      },
      contents: {
        type: 'string',
        description: 'For create/update: The C# script contents',
      },
      contentsEncoded: {
        type: 'boolean',
        description: 'For create/update: If true, contents is base64 encoded',
      },
      template: {
        type: 'string',
        enum: ['MonoBehaviour', 'ScriptableObject', 'Editor', 'EditorWindow', 'Empty'],
        description: 'For create: Script template to use (default: MonoBehaviour)',
      },
      className: {
        type: 'string',
        description: 'For create: Class name (defaults to filename without extension)',
      },
      namespace: {
        type: 'string',
        description: 'For create: Namespace for the script',
      },
      expectedSha: {
        type: 'string',
        description: 'For update: Expected SHA256 hash of current file (prevents conflicts)',
      },
      validationLevel: {
        type: 'string',
        enum: ['basic', 'standard', 'strict'],
        description: 'For validate: Validation level (basic=syntax, standard=structure, strict=full)',
      },
    },
    required: ['action', 'path'],
  },
};

// ============================================================================
// Tool Execution
// ============================================================================

export async function executeScriptTool(args: Record<string, unknown>): Promise<{
  content: Array<{ type: string; text: string }>;
  isError?: boolean;
}> {
  const action = args.action as string;
  const path = args.path as string;

  fileLog('INFO', 'ScriptTools', `Executing unity_script: ${action} on ${path}`);

  const client = getBridgeClient();

  try {
    const result = await client.call<Record<string, unknown>, unknown>(
      'unity_script',
      {
        action,
        path,
        contents: args.contents,
        contentsEncoded: args.contentsEncoded,
        template: args.template,
        className: args.className,
        namespace: args.namespace,
        expectedSha: args.expectedSha,
        validationLevel: args.validationLevel,
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
    fileLog('ERROR', 'ScriptTools', `unity_script failed: ${(error as Error).message}`);
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
