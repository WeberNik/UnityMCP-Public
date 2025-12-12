// ============================================================================
// UnityVision MCP Server - Batch Operation Tools
// Execute multiple operations in a single request
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export interface BatchOperation {
  id?: string;
  method: string;
  params: Record<string, unknown>;
}

export interface BatchExecuteInput {
  operations: BatchOperation[];
  stopOnError?: boolean;
  atomic?: boolean;
}

export interface BatchOperationResult {
  id: string;
  method: string;
  success: boolean;
  result?: unknown;
  error?: string;
  executionTimeMs: number;
}

export interface BatchExecuteOutput {
  success: boolean;
  totalOperations: number;
  successCount: number;
  failureCount: number;
  results: BatchOperationResult[];
  totalExecutionTimeMs: number;
}

export const batchToolDefinitions = [
  {
    name: 'batch_execute',
    description: `Execute multiple Unity operations in a single request. This is much faster than making individual calls.

Features:
- stopOnError: Stop execution on first error (default: false)
- atomic: Rollback all changes if any operation fails (default: false)

Example:
{
  "operations": [
    {"method": "create_game_object", "params": {"name": "Cube1"}},
    {"method": "create_game_object", "params": {"name": "Cube2"}},
    {"method": "modify_game_object", "params": {"path": "Cube1", "transform": {"position": {"x": 1, "y": 0, "z": 0}}}}
  ],
  "atomic": true
}`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        operations: {
          type: 'array',
          items: {
            type: 'object',
            properties: {
              id: {
                type: 'string',
                description: 'Optional ID for tracking this operation in results',
              },
              method: {
                type: 'string',
                description: 'Name of the method to call (e.g., "create_game_object")',
              },
              params: {
                type: 'object',
                description: 'Parameters for the method',
              },
            },
            required: ['method', 'params'],
          },
          description: 'Array of operations to execute',
        },
        stopOnError: {
          type: 'boolean',
          description: 'Stop execution on first error. Default is false.',
          default: false,
        },
        atomic: {
          type: 'boolean',
          description: 'If true, rollback all changes if any operation fails. Default is false.',
          default: false,
        },
      },
      required: ['operations'],
    },
  },
];

export async function batchExecute(params: BatchExecuteInput): Promise<BatchExecuteOutput> {
  const client = getBridgeClient();
  return client.call<BatchExecuteInput, BatchExecuteOutput>('batch_execute', {
    operations: params.operations,
    stopOnError: params.stopOnError ?? false,
    atomic: params.atomic ?? false,
  });
}
