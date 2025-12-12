// ============================================================================
// UnityVision MCP Server - Console Tools
// Tools for Unity console log management
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  GetConsoleLogsInput,
  GetConsoleLogsOutput,
  ClearConsoleLogsInput,
  ClearConsoleLogsOutput,
} from '../types.js';

export const consoleToolDefinitions = [
  {
    name: 'get_console_logs',
    description: 'Fetch recent Unity console logs. Filter by log level and optionally include stack traces. Useful for debugging errors and warnings.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        level: {
          type: 'string',
          enum: ['error', 'warning', 'info', 'all'],
          description: 'Filter logs by level. Default is "all".',
          default: 'all',
        },
        maxEntries: {
          type: 'number',
          description: 'Maximum number of log entries to return. Default is 200.',
          default: 200,
        },
        includeStackTrace: {
          type: 'boolean',
          description: 'Whether to include stack traces in the output. Default is true.',
          default: true,
        },
        sinceTimeMs: {
          type: 'number',
          description: 'Only return logs after this Unix timestamp in milliseconds. Default is 0 (all logs).',
          default: 0,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'clear_console_logs',
    description: 'Clear all Unity console logs.',
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
];

export async function getConsoleLogs(
  params: GetConsoleLogsInput
): Promise<GetConsoleLogsOutput> {
  const client = getBridgeClient();
  return client.call<GetConsoleLogsInput, GetConsoleLogsOutput>(
    'get_console_logs',
    {
      level: params.level ?? 'all',
      maxEntries: params.maxEntries ?? 200,
      includeStackTrace: params.includeStackTrace ?? true,
      sinceTimeMs: params.sinceTimeMs ?? 0,
    }
  );
}

export async function clearConsoleLogs(
  _params: ClearConsoleLogsInput
): Promise<ClearConsoleLogsOutput> {
  const client = getBridgeClient();
  return client.call<ClearConsoleLogsInput, ClearConsoleLogsOutput>(
    'clear_console_logs',
    {}
  );
}
