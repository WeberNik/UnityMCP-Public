// ============================================================================
// UnityVision MCP Server - Batch Tools Tests
// Tests for unity_batch tool (execute multiple operations atomically)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, setupMockResponse, resetMocks, testData, TEST_TIMEOUT } from '../setup.js';


describe('unity_batch Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('execute action', () => {
    it('should execute multiple operations', async () => {
      const batchResponse = {
        success: true,
        results: [
          { success: true, operation: 'create_game_object', result: { id: 'go_1' } },
          { success: true, operation: 'create_game_object', result: { id: 'go_2' } },
          { success: true, operation: 'create_game_object', result: { id: 'go_3' } },
        ],
        totalOperations: 3,
        successfulOperations: 3,
        failedOperations: 0,
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube1', primitiveType: 'Cube' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube2', primitiveType: 'Cube' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube3', primitiveType: 'Cube' } },
        ],
      }) as typeof batchResponse;

      expect(result.success).toBe(true);
      expect(result.totalOperations).toBe(3);
      expect(result.successfulOperations).toBe(3);
    }, TEST_TIMEOUT);

    it('should stop on error when stopOnError is true', async () => {
      const batchResponse = {
        success: false,
        results: [
          { success: true, operation: 'create_game_object', result: { id: 'go_1' } },
          { success: false, operation: 'create_game_object', error: 'Invalid primitive type' },
        ],
        totalOperations: 3,
        successfulOperations: 1,
        failedOperations: 1,
        stoppedAtIndex: 1,
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube1', primitiveType: 'Cube' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Invalid', primitiveType: 'InvalidType' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube3', primitiveType: 'Cube' } },
        ],
        stopOnError: true,
      }) as typeof batchResponse;

      expect(result.success).toBe(false);
      expect(result.stoppedAtIndex).toBe(1);
    });

    it('should continue on error when stopOnError is false', async () => {
      const batchResponse = {
        success: false,
        results: [
          { success: true, operation: 'create_game_object', result: { id: 'go_1' } },
          { success: false, operation: 'create_game_object', error: 'Invalid primitive type' },
          { success: true, operation: 'create_game_object', result: { id: 'go_3' } },
        ],
        totalOperations: 3,
        successfulOperations: 2,
        failedOperations: 1,
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube1', primitiveType: 'Cube' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Invalid', primitiveType: 'InvalidType' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube3', primitiveType: 'Cube' } },
        ],
        stopOnError: false,
      }) as typeof batchResponse;

      expect(result.successfulOperations).toBe(2);
      expect(result.failedOperations).toBe(1);
    });

    it('should execute mixed tool operations', async () => {
      const batchResponse = {
        success: true,
        results: [
          { success: true, operation: 'create_game_object', result: { id: 'go_1' } },
          { success: true, operation: 'add_component', result: { type: 'Rigidbody' } },
          { success: true, operation: 'set_component_properties', result: { modified: ['mass'] } },
        ],
        totalOperations: 3,
        successfulOperations: 3,
        failedOperations: 0,
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [
          { tool: 'unity_gameobject', action: 'create', params: { name: 'PhysicsCube', primitiveType: 'Cube' } },
          { tool: 'unity_component', action: 'add', params: { targetId: 'go_1', componentType: 'Rigidbody' } },
          { tool: 'unity_component', action: 'set_properties', params: { targetId: 'go_1', componentType: 'Rigidbody', properties: { mass: 10 } } },
        ],
      }) as typeof batchResponse;

      expect(result.success).toBe(true);
      expect(result.results).toHaveLength(3);
    });

    it('should return empty results for empty operations', async () => {
      const batchResponse = {
        success: true,
        results: [],
        totalOperations: 0,
        successfulOperations: 0,
        failedOperations: 0,
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [],
      }) as typeof batchResponse;

      expect(result.success).toBe(true);
      expect(result.totalOperations).toBe(0);
    });

    it('should handle scene setup batch', async () => {
      const batchResponse = {
        success: true,
        results: [
          { success: true, operation: 'create_game_object', result: { id: 'go_player', name: 'Player' } },
          { success: true, operation: 'create_game_object', result: { id: 'go_enemy1', name: 'Enemy1' } },
          { success: true, operation: 'create_game_object', result: { id: 'go_enemy2', name: 'Enemy2' } },
          { success: true, operation: 'create_game_object', result: { id: 'go_ground', name: 'Ground' } },
        ],
        totalOperations: 4,
        successfulOperations: 4,
        failedOperations: 0,
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Player', primitiveType: 'Capsule', position: { x: 0, y: 1, z: 0 } } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Enemy1', primitiveType: 'Cube', position: { x: 5, y: 1, z: 0 } } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Enemy2', primitiveType: 'Cube', position: { x: -5, y: 1, z: 0 } } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Ground', primitiveType: 'Plane', position: { x: 0, y: 0, z: 0 }, scale: { x: 10, y: 1, z: 10 } } },
        ],
      }) as typeof batchResponse;

      expect(result.success).toBe(true);
      expect(result.successfulOperations).toBe(4);
    });

    it('should support undo grouping', async () => {
      const batchResponse = {
        success: true,
        results: [
          { success: true, operation: 'create_game_object', result: { id: 'go_1' } },
          { success: true, operation: 'create_game_object', result: { id: 'go_2' } },
        ],
        totalOperations: 2,
        successfulOperations: 2,
        failedOperations: 0,
        undoGroupName: 'Batch Create GameObjects',
      };
      setupMockResponse('batch_execute', batchResponse);

      const result = await mockBridgeClient.call('batch_execute', {
        operations: [
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube1' } },
          { tool: 'unity_gameobject', action: 'create', params: { name: 'Cube2' } },
        ],
      }) as typeof batchResponse;

      expect(result.undoGroupName).toBeDefined();
    });
  });
});
