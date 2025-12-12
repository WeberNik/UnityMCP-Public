// ============================================================================
// UnityVision MCP Server - GameObject Tools Tests
// Tests for unity_gameobject tool (create, modify, delete)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, setupMockResponse, resetMocks, testData, TEST_TIMEOUT } from '../setup.js';


describe('unity_gameobject Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('create action', () => {
    it('should create empty GameObject', async () => {
      const createResponse = {
        success: true,
        gameObject: {
          id: testData.generateGameObjectId(),
          name: 'EmptyObject',
          path: 'EmptyObject',
          active: true,
        },
      };
      setupMockResponse('create_game_object', createResponse);

      const result = await mockBridgeClient.call('create_game_object', {
        name: 'EmptyObject',
      }) as typeof createResponse;

      expect(result.success).toBe(true);
      expect(result.gameObject.name).toBe('EmptyObject');
    }, TEST_TIMEOUT);

    it('should create primitive GameObject', async () => {
      const createResponse = {
        success: true,
        gameObject: {
          id: testData.generateGameObjectId(),
          name: 'MyCube',
          path: 'MyCube',
          active: true,
        },
      };
      setupMockResponse('create_game_object', createResponse);

      const result = await mockBridgeClient.call('create_game_object', {
        name: 'MyCube',
        primitiveType: 'Cube',
      }) as typeof createResponse;

      expect(result.success).toBe(true);
    });

    it('should create GameObject with position', async () => {
      const createResponse = {
        success: true,
        gameObject: {
          id: testData.generateGameObjectId(),
          name: 'PositionedObject',
          path: 'PositionedObject',
          active: true,
          position: { x: 1, y: 2, z: 3 },
        },
      };
      setupMockResponse('create_game_object', createResponse);

      const result = await mockBridgeClient.call('create_game_object', {
        name: 'PositionedObject',
        position: { x: 1, y: 2, z: 3 },
      }) as typeof createResponse;

      expect(result.success).toBe(true);
    });

    it('should create GameObject with rotation', async () => {
      const createResponse = {
        success: true,
        gameObject: {
          id: testData.generateGameObjectId(),
          name: 'RotatedObject',
          path: 'RotatedObject',
          active: true,
        },
      };
      setupMockResponse('create_game_object', createResponse);

      const result = await mockBridgeClient.call('create_game_object', {
        name: 'RotatedObject',
        rotation: { x: 0, y: 45, z: 0 },
      }) as typeof createResponse;

      expect(result.success).toBe(true);
    });

    it('should create GameObject with scale', async () => {
      const createResponse = {
        success: true,
        gameObject: {
          id: testData.generateGameObjectId(),
          name: 'ScaledObject',
          path: 'ScaledObject',
          active: true,
        },
      };
      setupMockResponse('create_game_object', createResponse);

      const result = await mockBridgeClient.call('create_game_object', {
        name: 'ScaledObject',
        scale: { x: 2, y: 2, z: 2 },
      }) as typeof createResponse;

      expect(result.success).toBe(true);
    });

    it('should create GameObject under parent', async () => {
      const createResponse = {
        success: true,
        gameObject: {
          id: testData.generateGameObjectId(),
          name: 'ChildObject',
          path: 'Parent/ChildObject',
          active: true,
        },
      };
      setupMockResponse('create_game_object', createResponse);

      const result = await mockBridgeClient.call('create_game_object', {
        name: 'ChildObject',
        parentId: 'go_12345',
      }) as typeof createResponse;

      expect(result.success).toBe(true);
      expect(result.gameObject.path).toContain('/');
    });

    it('should support all primitive types', async () => {
      const primitiveTypes = ['Cube', 'Sphere', 'Capsule', 'Cylinder', 'Plane', 'Quad'];

      for (const primitiveType of primitiveTypes) {
        const createResponse = {
          success: true,
          gameObject: {
            id: testData.generateGameObjectId(),
            name: primitiveType,
            path: primitiveType,
            active: true,
          },
        };
        setupMockResponse('create_game_object', createResponse);

        const result = await mockBridgeClient.call('create_game_object', {
          name: primitiveType,
          primitiveType,
        }) as typeof createResponse;

        expect(result.success).toBe(true);
      }
    });
  });

  describe('modify action', () => {
    it('should modify GameObject name', async () => {
      const modifyResponse = {
        success: true,
        gameObject: {
          id: 'go_12345',
          name: 'NewName',
          path: 'NewName',
          active: true,
        },
      };
      setupMockResponse('modify_game_object', modifyResponse);

      const result = await mockBridgeClient.call('modify_game_object', {
        targetId: 'go_12345',
        name: 'NewName',
      }) as typeof modifyResponse;

      expect(result.success).toBe(true);
      expect(result.gameObject.name).toBe('NewName');
    });

    it('should modify GameObject position', async () => {
      const modifyResponse = {
        success: true,
        gameObject: {
          id: 'go_12345',
          name: 'MovedObject',
          path: 'MovedObject',
          active: true,
        },
      };
      setupMockResponse('modify_game_object', modifyResponse);

      const result = await mockBridgeClient.call('modify_game_object', {
        targetId: 'go_12345',
        position: { x: 10, y: 20, z: 30 },
      }) as typeof modifyResponse;

      expect(result.success).toBe(true);
    });

    it('should modify GameObject active state', async () => {
      const modifyResponse = {
        success: true,
        gameObject: {
          id: 'go_12345',
          name: 'DisabledObject',
          path: 'DisabledObject',
          active: false,
        },
      };
      setupMockResponse('modify_game_object', modifyResponse);

      const result = await mockBridgeClient.call('modify_game_object', {
        targetId: 'go_12345',
        active: false,
      }) as typeof modifyResponse;

      expect(result.success).toBe(true);
      expect(result.gameObject.active).toBe(false);
    });

    it('should reparent GameObject', async () => {
      const modifyResponse = {
        success: true,
        gameObject: {
          id: 'go_12345',
          name: 'ReparentedObject',
          path: 'NewParent/ReparentedObject',
          active: true,
        },
      };
      setupMockResponse('modify_game_object', modifyResponse);

      const result = await mockBridgeClient.call('modify_game_object', {
        targetId: 'go_12345',
        parentId: 'go_99999',
      }) as typeof modifyResponse;

      expect(result.success).toBe(true);
    });

    it('should fail for non-existent GameObject', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('GameObject not found'));

      await expect(mockBridgeClient.call('modify_game_object', {
        targetId: 'go_nonexistent',
        name: 'NewName',
      })).rejects.toThrow('not found');
    });
  });

  describe('delete action', () => {
    it('should delete GameObject', async () => {
      const deleteResponse = {
        success: true,
        deletedId: 'go_12345',
        deletedPath: 'ObjectToDelete',
      };
      setupMockResponse('delete_game_object', deleteResponse);

      const result = await mockBridgeClient.call('delete_game_object', {
        targetId: 'go_12345',
      }) as typeof deleteResponse;

      expect(result.success).toBe(true);
      expect(result.deletedId).toBe('go_12345');
    });

    it('should delete GameObject and children', async () => {
      const deleteResponse = {
        success: true,
        deletedId: 'go_12345',
        deletedPath: 'Parent',
        childrenDeleted: 5,
      };
      setupMockResponse('delete_game_object', deleteResponse);

      const result = await mockBridgeClient.call('delete_game_object', {
        targetId: 'go_12345',
      }) as typeof deleteResponse;

      expect(result.success).toBe(true);
    });

    it('should fail for non-existent GameObject', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('GameObject not found'));

      await expect(mockBridgeClient.call('delete_game_object', {
        targetId: 'go_nonexistent',
      })).rejects.toThrow('not found');
    });

    it('should require targetId', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('targetId is required'));

      await expect(mockBridgeClient.call('delete_game_object', {}))
        .rejects.toThrow('required');
    });
  });
});
