// ============================================================================
// UnityVision MCP Server - Code Execution Tools
// Runtime C# code execution in Unity
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export interface ExecuteCodeInput {
  code: string;
  captureOutput?: boolean;
  timeoutMs?: number;
}

export interface ExecuteCodeOutput {
  success: boolean;
  result?: unknown;
  resultType?: string;
  output?: string;
  error?: string;
  executionTimeMs?: number;
}

export interface EvaluateExpressionInput {
  expression: string;
}

export interface EvaluateExpressionOutput {
  success: boolean;
  result?: unknown;
  resultType?: string;
  error?: string;
}

export const codeExecutionToolDefinitions = [
  {
    name: 'execute_code',
    description: `Execute arbitrary C# code in the Unity runtime. This is the most powerful tool - use it when no other tool fits your needs. The code runs in the Unity Editor context with access to all Unity APIs.

Examples:
- "GameObject.Find(\\"Player\\").transform.position = Vector3.zero;"
- "Selection.activeGameObject = GameObject.Find(\\"Main Camera\\");"
- "Debug.Log(\\"Hello from AI!\\");"
- "return FindObjectsOfType<Light>().Length;"

The code can use: UnityEngine, UnityEditor, System, System.Linq, System.Collections.Generic`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        code: {
          type: 'string',
          description: 'C# code to execute. Can be statements or an expression. Use "return X;" to return a value.',
        },
        captureOutput: {
          type: 'boolean',
          description: 'Capture Debug.Log output. Default is true.',
          default: true,
        },
        timeoutMs: {
          type: 'number',
          description: 'Timeout in milliseconds. Default is 5000.',
          default: 5000,
        },
      },
      required: ['code'],
    },
  },
  {
    name: 'evaluate_expression',
    description: `Evaluate a C# expression and return its value. Simpler than execute_code for quick queries.

Examples:
- "Camera.main.transform.position"
- "Selection.activeGameObject?.name"
- "FindObjectsOfType<Rigidbody>().Length"
- "Application.isPlaying"`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        expression: {
          type: 'string',
          description: 'C# expression to evaluate. Must return a value.',
        },
      },
      required: ['expression'],
    },
  },
];

export async function executeCode(
  params: ExecuteCodeInput
): Promise<ExecuteCodeOutput> {
  const client = getBridgeClient();
  return client.call<ExecuteCodeInput, ExecuteCodeOutput>(
    'execute_code',
    {
      code: params.code,
      captureOutput: params.captureOutput ?? true,
      timeoutMs: params.timeoutMs ?? 5000,
    }
  );
}

export async function evaluateExpression(
  params: EvaluateExpressionInput
): Promise<EvaluateExpressionOutput> {
  const client = getBridgeClient();
  return client.call<EvaluateExpressionInput, EvaluateExpressionOutput>(
    'evaluate_expression',
    params
  );
}
