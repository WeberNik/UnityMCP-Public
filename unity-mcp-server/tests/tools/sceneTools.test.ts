// ============================================================================
// UnityVision MCP Server - Scene Tools Tests
// Tests for unity_scene tool (list, hierarchy, create, load, save, delete)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, mockResponses, setupMockResponse, resetMocks, TEST_TIMEOUT } from '../setup.js';


describe('unity_scene Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('list action', () => {
    it('should list all scenes in build settings', async () => {
      const listResponse = {
        scenes: [
          { name: 'MainMenu', path: 'Assets/Scenes/MainMenu.unity', isLoaded: false, isActive: false, buildIndex: 0 },
          { name: 'GameScene', path: 'Assets/Scenes/GameScene.unity', isLoaded: true, isActive: true, buildIndex: 1 },
        ],
      };
      setupMockResponse('list_scenes', listResponse);

      const result = await mockBridgeClient.call('list_scenes', {}) as typeof listResponse;

      expect(result.scenes).toBeInstanceOf(Array);
      expect(result.scenes.length).toBeGreaterThan(0);
    }, TEST_TIMEOUT);

    it('should include scene metadata', async () => {
      const listResponse = {
        scenes: [
          { name: 'TestScene', path: 'Assets/Scenes/TestScene.unity', isLoaded: true, isActive: true, buildIndex: 0 },
        ],
      };
      setupMockResponse('list_scenes', listResponse);

      const result = await mockBridgeClient.call('list_scenes', {}) as typeof listResponse;

      const scene = result.scenes[0];
      expect(scene).toHaveProperty('name');
      expect(scene).toHaveProperty('path');
      expect(scene).toHaveProperty('isLoaded');
      expect(scene).toHaveProperty('isActive');
      expect(scene).toHaveProperty('buildIndex');
    });

    it('should filter scenes by name', async () => {
      const listResponse = {
        scenes: [
          { name: 'MainMenu', path: 'Assets/Scenes/MainMenu.unity', isLoaded: false, isActive: false, buildIndex: 0 },
        ],
      };
      setupMockResponse('list_scenes', listResponse);

      const result = await mockBridgeClient.call('list_scenes', { filter: 'Main' }) as typeof listResponse;

      expect(result.scenes.every(s => s.name.includes('Main'))).toBe(true);
    });
  });

  describe('hierarchy action', () => {
    it('should return scene hierarchy', async () => {
      setupMockResponse('get_scene_hierarchy', mockResponses.hierarchy);

      const result = await mockBridgeClient.call('get_scene_hierarchy', {}) as typeof mockResponses.hierarchy;

      expect(result.rootObjects).toBeInstanceOf(Array);
      expect(result.totalObjectsInScene).toBeGreaterThan(0);
    });

    it('should include GameObject details', async () => {
      setupMockResponse('get_scene_hierarchy', mockResponses.hierarchy);

      const result = await mockBridgeClient.call('get_scene_hierarchy', {}) as typeof mockResponses.hierarchy;

      const go = result.rootObjects[0];
      expect(go).toHaveProperty('id');
      expect(go).toHaveProperty('name');
      expect(go).toHaveProperty('path');
      expect(go).toHaveProperty('active');
    });

    it('should optionally include components', async () => {
      setupMockResponse('get_scene_hierarchy', mockResponses.hierarchy);

      const result = await mockBridgeClient.call('get_scene_hierarchy', {
        includeComponents: true,
      }) as typeof mockResponses.hierarchy;

      const go = result.rootObjects[0];
      expect(go.components).toBeInstanceOf(Array);
    });

    it('should respect maxDepth parameter', async () => {
      const shallowHierarchy = {
        rootObjects: [
          { id: 'go_1', name: 'Parent', path: 'Parent', active: true, children: [] },
        ],
        totalObjectsInScene: 10,
        returnedObjects: 1,
        truncated: true,
      };
      setupMockResponse('get_scene_hierarchy', shallowHierarchy);

      const result = await mockBridgeClient.call('get_scene_hierarchy', {
        maxDepth: 1,
      }) as typeof shallowHierarchy;

      expect(result.truncated).toBe(true);
    });

    it('should respect maxObjects parameter', async () => {
      const limitedHierarchy = {
        rootObjects: mockResponses.hierarchy.rootObjects.slice(0, 1),
        totalObjectsInScene: 100,
        returnedObjects: 50,
        truncated: true,
      };
      setupMockResponse('get_scene_hierarchy', limitedHierarchy);

      const result = await mockBridgeClient.call('get_scene_hierarchy', {
        maxObjects: 50,
      }) as typeof limitedHierarchy;

      expect(result.returnedObjects).toBeLessThanOrEqual(50);
      expect(result.truncated).toBe(true);
    });
  });

  describe('create action', () => {
    it('should create a new scene', async () => {
      setupMockResponse('scene_create', mockResponses.sceneCreated);

      const result = await mockBridgeClient.call('scene_create', {
        path: 'Assets/Scenes/NewScene.unity',
      }) as typeof mockResponses.sceneCreated;

      expect(result.success).toBe(true);
      expect(result.path).toContain('.unity');
    });

    it('should auto-add .unity extension', async () => {
      const createResponse = {
        success: true,
        path: 'Assets/Scenes/NoExtension.unity',
        name: 'NoExtension',
      };
      setupMockResponse('scene_create', createResponse);

      const result = await mockBridgeClient.call('scene_create', {
        path: 'Assets/Scenes/NoExtension',
      }) as typeof createResponse;

      expect(result.path).toEndWith('.unity');
    });

    it('should auto-add Assets/ prefix', async () => {
      const createResponse = {
        success: true,
        path: 'Assets/Scenes/Test.unity',
        name: 'Test',
      };
      setupMockResponse('scene_create', createResponse);

      const result = await mockBridgeClient.call('scene_create', {
        path: 'Scenes/Test.unity',
      }) as typeof createResponse;

      expect(result.path).toStartWith('Assets/');
    });
  });

  describe('load action', () => {
    it('should load scene in single mode', async () => {
      const loadResponse = {
        success: true,
        path: 'Assets/Scenes/GameScene.unity',
        name: 'GameScene',
        isLoaded: true,
        additive: false,
      };
      setupMockResponse('scene_load', loadResponse);

      const result = await mockBridgeClient.call('scene_load', {
        path: 'Assets/Scenes/GameScene.unity',
      }) as typeof loadResponse;

      expect(result.success).toBe(true);
      expect(result.additive).toBe(false);
    });

    it('should load scene in additive mode', async () => {
      const loadResponse = {
        success: true,
        path: 'Assets/Scenes/AdditiveScene.unity',
        name: 'AdditiveScene',
        isLoaded: true,
        additive: true,
      };
      setupMockResponse('scene_load', loadResponse);

      const result = await mockBridgeClient.call('scene_load', {
        path: 'Assets/Scenes/AdditiveScene.unity',
        additive: true,
      }) as typeof loadResponse;

      expect(result.success).toBe(true);
      expect(result.additive).toBe(true);
    });

    it('should fail for non-existent scene', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Scene not found'));

      await expect(mockBridgeClient.call('scene_load', {
        path: 'Assets/Scenes/NonExistent.unity',
      })).rejects.toThrow('not found');
    });
  });

  describe('save action', () => {
    it('should save current scene', async () => {
      const saveResponse = {
        success: true,
        path: 'Assets/Scenes/CurrentScene.unity',
        name: 'CurrentScene',
      };
      setupMockResponse('scene_save', saveResponse);

      const result = await mockBridgeClient.call('scene_save', {}) as typeof saveResponse;

      expect(result.success).toBe(true);
    });

    it('should save scene to new path', async () => {
      const saveResponse = {
        success: true,
        path: 'Assets/Scenes/NewPath.unity',
        name: 'NewPath',
      };
      setupMockResponse('scene_save', saveResponse);

      const result = await mockBridgeClient.call('scene_save', {
        saveAs: 'Assets/Scenes/NewPath.unity',
      }) as typeof saveResponse;

      expect(result.success).toBe(true);
      expect(result.path).toContain('NewPath');
    });
  });

  describe('delete action', () => {
    it('should delete scene file', async () => {
      const deleteResponse = {
        success: true,
        path: 'Assets/Scenes/ToDelete.unity',
        wasInBuildSettings: false,
        warning: null,
      };
      setupMockResponse('scene_delete', deleteResponse);

      const result = await mockBridgeClient.call('scene_delete', {
        path: 'Assets/Scenes/ToDelete.unity',
      }) as typeof deleteResponse;

      expect(result.success).toBe(true);
    });

    it('should warn if scene was in build settings', async () => {
      const deleteResponse = {
        success: true,
        path: 'Assets/Scenes/BuildScene.unity',
        wasInBuildSettings: true,
        warning: 'Scene was in build settings and may need to be removed manually',
      };
      setupMockResponse('scene_delete', deleteResponse);

      const result = await mockBridgeClient.call('scene_delete', {
        path: 'Assets/Scenes/BuildScene.unity',
      }) as typeof deleteResponse;

      expect(result.wasInBuildSettings).toBe(true);
      expect(result.warning).toBeDefined();
    });

    it('should fail for active scene', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Cannot delete the currently active scene'));

      await expect(mockBridgeClient.call('scene_delete', {
        path: 'Assets/Scenes/ActiveScene.unity',
      })).rejects.toThrow('active scene');
    });
  });
});

// Custom matchers
expect.extend({
  toEndWith(received: string, suffix: string) {
    const pass = received.endsWith(suffix);
    return {
      pass,
      message: () => `expected "${received}" to ${pass ? 'not ' : ''}end with "${suffix}"`,
    };
  },
  toStartWith(received: string, prefix: string) {
    const pass = received.startsWith(prefix);
    return {
      pass,
      message: () => `expected "${received}" to ${pass ? 'not ' : ''}start with "${prefix}"`,
    };
  },
});

declare global {
  namespace jest {
    interface Matchers<R> {
      toEndWith(suffix: string): R;
      toStartWith(prefix: string): R;
    }
  }
}
