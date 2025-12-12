// ============================================================================
// UnityVision MCP Server - Test Setup
// Common test utilities and mocks
// ============================================================================

import { jest } from '@jest/globals';

// Mock the Unity bridge client
export const mockBridgeClient = {
  call: jest.fn<(method: string, params?: unknown) => Promise<unknown>>(),
  connect: jest.fn<() => Promise<void>>(),
  disconnect: jest.fn<() => void>(),
  isConnected: jest.fn<() => boolean>().mockReturnValue(true),
};

// Mock successful responses
export const mockResponses = {
  editorState: {
    unityVersion: '2022.3.10f1',
    projectPath: 'C:/Projects/TestProject',
    isPlaying: false,
    isPaused: false,
    activeScene: 'SampleScene',
    loadedScenes: ['SampleScene'],
    platform: 'WindowsEditor',
  },

  consoleLog: {
    logs: [
      { timestamp: Date.now(), type: 'Log', message: 'Test log message', stackTrace: '' },
      { timestamp: Date.now(), type: 'Warning', message: 'Test warning', stackTrace: '' },
      { timestamp: Date.now(), type: 'Error', message: 'Test error', stackTrace: 'at TestScript.cs:10' },
    ],
    totalCount: 3,
  },

  hierarchy: {
    rootObjects: [
      {
        id: 'go_12345',
        name: 'Main Camera',
        path: 'Main Camera',
        active: true,
        components: ['Transform', 'Camera', 'AudioListener'],
        children: [],
      },
      {
        id: 'go_12346',
        name: 'Directional Light',
        path: 'Directional Light',
        active: true,
        components: ['Transform', 'Light'],
        children: [],
      },
    ],
    totalObjectsInScene: 2,
    returnedObjects: 2,
    truncated: false,
  },

  gameObjectCreated: {
    success: true,
    gameObject: {
      id: 'go_99999',
      name: 'TestCube',
      path: 'TestCube',
      active: true,
    },
  },

  scriptCreated: {
    success: true,
    path: 'Assets/Scripts/TestScript.cs',
    className: 'TestScript',
    sha256: 'abc123def456',
  },

  packageList: {
    success: true,
    count: 3,
    packages: [
      { name: 'com.unity.textmeshpro', displayName: 'TextMeshPro', version: '3.0.6' },
      { name: 'com.unity.inputsystem', displayName: 'Input System', version: '1.7.0' },
      { name: 'com.unity.cinemachine', displayName: 'Cinemachine', version: '2.9.7' },
    ],
  },

  sceneCreated: {
    success: true,
    path: 'Assets/Scenes/TestScene.unity',
    name: 'TestScene',
  },

  recompileResult: {
    success: true,
    message: 'Script recompilation requested',
    isCompiling: true,
  },

  refreshResult: {
    success: true,
    message: 'Asset database refreshed',
  },
};

// Helper to setup mock responses
export function setupMockResponse(method: string, response: unknown) {
  mockBridgeClient.call.mockImplementation((m: unknown) => {
    const methodStr = String(m);
    if (methodStr === method || methodStr.includes(method)) {
      return Promise.resolve(response);
    }
    return Promise.reject(new Error(`Unexpected method: ${methodStr}`));
  });
}

// Helper to setup multiple mock responses
export function setupMockResponses(responses: Record<string, unknown>) {
  mockBridgeClient.call.mockImplementation((m: unknown) => {
    const methodStr = String(m);
    for (const [key, value] of Object.entries(responses)) {
      if (methodStr === key || methodStr.includes(key)) {
        return Promise.resolve(value);
      }
    }
    return Promise.reject(new Error(`Unexpected method: ${methodStr}`));
  });
}

// Reset all mocks
export function resetMocks() {
  jest.clearAllMocks();
}

// Timeout helper for async tests
export const TEST_TIMEOUT = 10000;

// Test data generators
export const testData = {
  generateGameObjectId: () => `go_${Math.floor(Math.random() * 100000)}`,
  generateScriptPath: (name: string) => `Assets/Scripts/${name}.cs`,
  generateScenePath: (name: string) => `Assets/Scenes/${name}.unity`,
  
  sampleScript: `using UnityEngine;

public class TestScript : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Hello from TestScript!");
    }

    void Update()
    {
    }
}`,

  sampleMonoBehaviour: (className: string) => `using UnityEngine;

public class ${className} : MonoBehaviour
{
    void Start()
    {
    }

    void Update()
    {
    }
}`,
};
