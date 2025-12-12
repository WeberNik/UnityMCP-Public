// ============================================================================
// UnityVision MCP Server - Build Tools
// Tools for building Unity players
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  BuildPlayerInput,
  BuildPlayerOutput,
} from '../types.js';

export const buildToolDefinitions = [
  {
    name: 'build_player',
    description: 'Build a Unity player for a target platform. Returns build status, path, and any errors or warnings.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        targetPlatform: {
          type: 'string',
          enum: ['Android', 'iOS', 'StandaloneWindows64', 'StandaloneOSX'],
          description: 'Target platform to build for.',
        },
        buildPath: {
          type: 'string',
          description: 'Output path for the build. Uses default if not specified.',
        },
        developmentBuild: {
          type: 'boolean',
          description: 'Whether to create a development build with debugging enabled.',
          default: false,
        },
        buildOptions: {
          type: 'array',
          items: { type: 'string' },
          description: 'Additional build options (e.g., "CompressWithLz4", "StrictMode").',
        },
        scenes: {
          type: 'array',
          items: { type: 'string' },
          description: 'List of scene paths to include. Uses build settings if not specified.',
        },
      },
      required: ['targetPlatform'],
    },
  },
];

export async function buildPlayer(
  params: BuildPlayerInput
): Promise<BuildPlayerOutput> {
  const client = getBridgeClient();
  return client.call<BuildPlayerInput, BuildPlayerOutput>(
    'build_player',
    {
      targetPlatform: params.targetPlatform,
      buildPath: params.buildPath ?? '',
      developmentBuild: params.developmentBuild ?? false,
      buildOptions: params.buildOptions ?? [],
      scenes: params.scenes ?? [],
    }
  );
}
