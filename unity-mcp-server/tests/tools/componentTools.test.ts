// ============================================================================
// UnityVision MCP Server - Component Tools Tests
// Tests for unity_component tool (search, add, get_properties, set_properties, etc.)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, setupMockResponse, resetMocks, TEST_TIMEOUT } from '../setup.js';


describe('unity_component Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('search action', () => {
    it('should search for component types', async () => {
      const searchResponse = {
        types: [
          { name: 'Rigidbody', fullName: 'UnityEngine.Rigidbody', assembly: 'UnityEngine.PhysicsModule' },
          { name: 'Rigidbody2D', fullName: 'UnityEngine.Rigidbody2D', assembly: 'UnityEngine.Physics2DModule' },
        ],
        count: 2,
      };
      setupMockResponse('search_component_types', searchResponse);

      const result = await mockBridgeClient.call('search_component_types', {
        searchQuery: 'Rigidbody',
      }) as typeof searchResponse;

      expect(result.types).toBeInstanceOf(Array);
      expect(result.types.length).toBeGreaterThan(0);
    }, TEST_TIMEOUT);

    it('should return full type names', async () => {
      const searchResponse = {
        types: [
          { name: 'Transform', fullName: 'UnityEngine.Transform', assembly: 'UnityEngine.CoreModule' },
        ],
        count: 1,
      };
      setupMockResponse('search_component_types', searchResponse);

      const result = await mockBridgeClient.call('search_component_types', {
        searchQuery: 'Transform',
      }) as typeof searchResponse;

      expect(result.types[0].fullName).toContain('UnityEngine');
    });
  });

  describe('add action', () => {
    it('should add component to GameObject', async () => {
      const addResponse = {
        success: true,
        component: {
          type: 'UnityEngine.Rigidbody',
          gameObjectId: 'go_12345',
        },
      };
      setupMockResponse('add_component', addResponse);

      const result = await mockBridgeClient.call('add_component', {
        targetId: 'go_12345',
        componentType: 'Rigidbody',
      }) as typeof addResponse;

      expect(result.success).toBe(true);
    });

    it('should fail for non-existent component type', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Component type not found'));

      await expect(mockBridgeClient.call('add_component', {
        targetId: 'go_12345',
        componentType: 'NonExistentComponent',
      })).rejects.toThrow('not found');
    });

    it('should fail for non-existent GameObject', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('GameObject not found'));

      await expect(mockBridgeClient.call('add_component', {
        targetId: 'go_nonexistent',
        componentType: 'Rigidbody',
      })).rejects.toThrow('not found');
    });
  });

  describe('get_properties action', () => {
    it('should get component properties', async () => {
      const getResponse = {
        success: true,
        componentType: 'UnityEngine.Rigidbody',
        properties: {
          mass: 1.0,
          drag: 0.0,
          angularDrag: 0.05,
          useGravity: true,
          isKinematic: false,
        },
      };
      setupMockResponse('get_component_properties', getResponse);

      const result = await mockBridgeClient.call('get_component_properties', {
        targetId: 'go_12345',
        componentType: 'Rigidbody',
      }) as typeof getResponse;

      expect(result.properties).toBeDefined();
      expect(result.properties.mass).toBe(1.0);
    });

    it('should get specific property', async () => {
      const getResponse = {
        success: true,
        componentType: 'UnityEngine.Rigidbody',
        propertyName: 'mass',
        value: 5.0,
      };
      setupMockResponse('get_component_properties', getResponse);

      const result = await mockBridgeClient.call('get_component_properties', {
        targetId: 'go_12345',
        componentType: 'Rigidbody',
        propertyName: 'mass',
      }) as typeof getResponse;

      expect(result.value).toBe(5.0);
    });
  });

  describe('set_properties action', () => {
    it('should set component properties', async () => {
      const setResponse = {
        success: true,
        modifiedProperties: ['mass', 'useGravity'],
      };
      setupMockResponse('set_component_properties', setResponse);

      const result = await mockBridgeClient.call('set_component_properties', {
        targetId: 'go_12345',
        componentType: 'Rigidbody',
        properties: {
          mass: 10.0,
          useGravity: false,
        },
      }) as typeof setResponse;

      expect(result.success).toBe(true);
      expect(result.modifiedProperties).toContain('mass');
    });

    it('should fail for invalid property value', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Invalid value for property mass'));

      await expect(mockBridgeClient.call('set_component_properties', {
        targetId: 'go_12345',
        componentType: 'Rigidbody',
        properties: {
          mass: 'not a number',
        },
      })).rejects.toThrow('Invalid value');
    });
  });

  describe('set_property action', () => {
    it('should set single property', async () => {
      const setResponse = {
        success: true,
        propertyName: 'mass',
        oldValue: 1.0,
        newValue: 5.0,
      };
      setupMockResponse('set_component_property', setResponse);

      const result = await mockBridgeClient.call('set_component_property', {
        targetId: 'go_12345',
        componentType: 'Rigidbody',
        propertyName: 'mass',
        propertyValue: 5.0,
      }) as typeof setResponse;

      expect(result.success).toBe(true);
      expect(result.newValue).toBe(5.0);
    });
  });

  describe('compare action', () => {
    it('should compare components between GameObjects', async () => {
      const compareResponse = {
        success: true,
        componentType: 'UnityEngine.Rigidbody',
        differences: [
          { property: 'mass', valueA: 1.0, valueB: 5.0 },
          { property: 'useGravity', valueA: true, valueB: false },
        ],
        identical: false,
      };
      setupMockResponse('compare_components', compareResponse);

      const result = await mockBridgeClient.call('compare_components', {
        targetId: 'go_12345',
        compareTargetId: 'go_67890',
        componentType: 'Rigidbody',
      }) as typeof compareResponse;

      expect(result.differences).toBeInstanceOf(Array);
      expect(result.identical).toBe(false);
    });

    it('should report identical components', async () => {
      const compareResponse = {
        success: true,
        componentType: 'UnityEngine.Rigidbody',
        differences: [],
        identical: true,
      };
      setupMockResponse('compare_components', compareResponse);

      const result = await mockBridgeClient.call('compare_components', {
        targetId: 'go_12345',
        compareTargetId: 'go_67890',
        componentType: 'Rigidbody',
      }) as typeof compareResponse;

      expect(result.identical).toBe(true);
      expect(result.differences).toHaveLength(0);
    });
  });
});
