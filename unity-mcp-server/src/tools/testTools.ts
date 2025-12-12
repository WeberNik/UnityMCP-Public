// ============================================================================
// UnityVision MCP Server - Test Tools
// Tools for running Unity tests
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  RunTestsInput,
  RunTestsOutput,
} from '../types.js';

export const testToolDefinitions = [
  {
    name: 'run_tests',
    description: 'Run Unity Test Framework tests (EditMode and/or PlayMode). Returns a summary of test results including failed tests with error messages.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        testMode: {
          type: 'string',
          enum: ['EditMode', 'PlayMode', 'Both'],
          description: 'Which test mode to run.',
        },
        filter: {
          type: 'string',
          description: 'Optional filter to run only tests matching this namespace or name pattern.',
        },
        timeout: {
          type: 'number',
          description: 'Timeout in seconds for the test run. Default is 300 (5 minutes).',
          default: 300,
        },
      },
      required: ['testMode'],
    },
  },
];

export async function runTests(
  params: RunTestsInput
): Promise<RunTestsOutput> {
  const client = getBridgeClient();
  return client.call<RunTestsInput, RunTestsOutput>(
    'run_tests',
    {
      testMode: params.testMode,
      filter: params.filter ?? '',
      timeout: params.timeout ?? 300,
    }
  );
}
