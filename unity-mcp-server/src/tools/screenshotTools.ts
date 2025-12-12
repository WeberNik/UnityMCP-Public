// ============================================================================
// UnityVision MCP Server - Screenshot Tools
// Tools for capturing Game View and Scene View screenshots
// Supports auto-creating cameras from Scene View when no camera exists
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  CaptureGameViewScreenshotInput,
  CaptureGameViewScreenshotOutput,
  CaptureSceneViewScreenshotInput,
  CaptureSceneViewScreenshotOutput,
} from '../types.js';

export const screenshotToolDefinitions = [
  {
    name: 'capture_game_view_screenshot',
    description: `Capture a screenshot of the Unity Game View. Returns the image as base64-encoded PNG.

**Smart Camera Detection:**
- Uses the main camera if available
- Falls back to any active camera in the scene
- If no camera exists, automatically creates a temporary camera matching the current Scene View perspective
- Response includes 'cameraSource' field indicating which camera was used

**Use Cases:**
- "Show me what the game looks like" - captures from main camera
- "Check the UI layout" - captures the rendered game view
- Works even in scenes without a camera (uses Scene View perspective)`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        resolutionWidth: {
          type: 'number',
          description: 'Width of the screenshot in pixels. Default is 1280.',
          default: 1280,
        },
        resolutionHeight: {
          type: 'number',
          description: 'Height of the screenshot in pixels. Default is 720.',
          default: 720,
        },
        superSampling: {
          type: 'number',
          description: 'Super-sampling multiplier for higher quality. Default is 1.',
          default: 1,
        },
        camera: {
          type: 'string',
          description: 'Name or path of the camera to use. Uses main camera if not specified.',
        },
        includeGizmos: {
          type: 'boolean',
          description: 'Whether to include editor gizmos in the screenshot. Default is false.',
          default: false,
        },
        format: {
          type: 'string',
          enum: ['png_base64', 'jpg_base64'],
          description: 'Output format. Default is png_base64.',
          default: 'png_base64',
        },
        createFromSceneView: {
          type: 'boolean',
          description: 'If true and no camera exists in the scene, create a temporary camera matching the Scene View perspective. Default is true.',
          default: true,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'capture_scene_view_screenshot',
    description: `Capture a screenshot of the Unity Scene View - exactly what the user sees in the editor.

**Features:**
- Captures the current Scene View perspective (position, rotation, zoom)
- Can focus on a specific GameObject before capturing
- Can set explicit camera position/rotation
- Response includes camera info (position, rotation, FOV, orthographic settings)

**Use Cases:**
- "Check the scene view and tell me how to improve the UI" - captures editor perspective
- "Show me the current scene layout" - captures what user is looking at
- "Focus on the Player object and take a screenshot" - frames specific object`,
    inputSchema: {
      type: 'object' as const,
      properties: {
        resolutionWidth: {
          type: 'number',
          description: 'Width of the screenshot in pixels. Default is 1280.',
          default: 1280,
        },
        resolutionHeight: {
          type: 'number',
          description: 'Height of the screenshot in pixels. Default is 720.',
          default: 720,
        },
        focusObjectPath: {
          type: 'string',
          description: 'Path to a GameObject to focus the scene camera on before capturing.',
        },
        cameraPosition: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Optional explicit camera position.',
        },
        cameraRotation: {
          type: 'object',
          properties: {
            x: { type: 'number' },
            y: { type: 'number' },
            z: { type: 'number' },
          },
          description: 'Optional explicit camera rotation (Euler angles).',
        },
        format: {
          type: 'string',
          enum: ['png_base64', 'jpg_base64'],
          description: 'Output format. Default is png_base64.',
          default: 'png_base64',
        },
        captureEditorView: {
          type: 'boolean',
          description: 'If true, attempts to capture exactly what the user sees including grid and gizmos. Default is true.',
          default: true,
        },
      },
      required: [] as string[],
    },
  },
];

export async function captureGameViewScreenshot(
  params: CaptureGameViewScreenshotInput
): Promise<CaptureGameViewScreenshotOutput> {
  const client = getBridgeClient();
  return client.call<CaptureGameViewScreenshotInput, CaptureGameViewScreenshotOutput>(
    'capture_game_view_screenshot',
    {
      resolutionWidth: params.resolutionWidth ?? 1280,
      resolutionHeight: params.resolutionHeight ?? 720,
      superSampling: params.superSampling ?? 1,
      camera: params.camera ?? '',
      includeGizmos: params.includeGizmos ?? false,
      format: params.format ?? 'png_base64',
      createFromSceneView: params.createFromSceneView ?? true,
    }
  );
}

export async function captureSceneViewScreenshot(
  params: CaptureSceneViewScreenshotInput
): Promise<CaptureSceneViewScreenshotOutput> {
  const client = getBridgeClient();
  return client.call<CaptureSceneViewScreenshotInput, CaptureSceneViewScreenshotOutput>(
    'capture_scene_view_screenshot',
    {
      resolutionWidth: params.resolutionWidth ?? 1280,
      resolutionHeight: params.resolutionHeight ?? 720,
      focusObjectPath: params.focusObjectPath ?? '',
      cameraPosition: params.cameraPosition,
      cameraRotation: params.cameraRotation,
      format: params.format ?? 'png_base64',
      captureEditorView: params.captureEditorView ?? true,
    }
  );
}
