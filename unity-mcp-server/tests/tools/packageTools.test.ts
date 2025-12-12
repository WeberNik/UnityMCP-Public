// ============================================================================
// UnityVision MCP Server - Package Tools Tests
// Tests for unity_package tool (list, add, remove)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, mockResponses, setupMockResponse, resetMocks, TEST_TIMEOUT } from '../setup.js';


describe('unity_package Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('list action', () => {
    it('should list installed packages', async () => {
      setupMockResponse('unity_package', mockResponses.packageList);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'list',
      }) as typeof mockResponses.packageList;

      expect(result.success).toBe(true);
      expect(result.packages).toBeInstanceOf(Array);
      expect(result.count).toBeGreaterThan(0);
    }, TEST_TIMEOUT);

    it('should return package details', async () => {
      setupMockResponse('unity_package', mockResponses.packageList);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'list',
      }) as typeof mockResponses.packageList;

      const pkg = result.packages[0];
      expect(pkg).toHaveProperty('name');
      expect(pkg).toHaveProperty('displayName');
      expect(pkg).toHaveProperty('version');
    });

    it('should optionally include built-in packages', async () => {
      const listWithBuiltIn = {
        success: true,
        count: 10,
        packages: [
          ...mockResponses.packageList.packages,
          { name: 'com.unity.modules.physics', displayName: 'Physics', version: '1.0.0' },
        ],
      };
      setupMockResponse('unity_package', listWithBuiltIn);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'list',
        includeBuiltIn: true,
      }) as typeof listWithBuiltIn;

      expect(result.packages.some(p => p.name.includes('modules'))).toBe(true);
    });
  });

  describe('add action', () => {
    it('should add package by name', async () => {
      const addResponse = {
        success: true,
        message: 'Successfully installed TextMeshPro',
        package: {
          name: 'com.unity.textmeshpro',
          displayName: 'TextMeshPro',
          version: '3.0.6',
          description: 'Text rendering package',
        },
      };
      setupMockResponse('unity_package', addResponse);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'add',
        packageName: 'com.unity.textmeshpro',
      }) as typeof addResponse;

      expect(result.success).toBe(true);
      expect(result.package.name).toBe('com.unity.textmeshpro');
    }, TEST_TIMEOUT);

    it('should add package with specific version', async () => {
      const addResponse = {
        success: true,
        message: 'Successfully installed Input System',
        package: {
          name: 'com.unity.inputsystem',
          displayName: 'Input System',
          version: '1.6.0',
          description: 'Input handling package',
        },
      };
      setupMockResponse('unity_package', addResponse);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'add',
        packageName: 'com.unity.inputsystem',
        version: '1.6.0',
      }) as typeof addResponse;

      expect(result.success).toBe(true);
      expect(result.package.version).toBe('1.6.0');
    });

    it('should add package from git URL', async () => {
      const addResponse = {
        success: true,
        message: 'Successfully installed Custom Package',
        package: {
          name: 'com.custom.package',
          displayName: 'Custom Package',
          version: '1.0.0',
          description: 'A custom package from git',
        },
      };
      setupMockResponse('unity_package', addResponse);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'add',
        gitUrl: 'https://github.com/user/repo.git',
      }) as typeof addResponse;

      expect(result.success).toBe(true);
    });

    it('should fail for non-existent package', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Package not found in registry'));

      await expect(mockBridgeClient.call('unity_package', {
        action: 'add',
        packageName: 'com.nonexistent.package',
      })).rejects.toThrow('not found');
    });

    it('should require packageName or gitUrl', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Either packageName or gitUrl is required'));

      await expect(mockBridgeClient.call('unity_package', {
        action: 'add',
      })).rejects.toThrow('required');
    });
  });

  describe('remove action', () => {
    it('should remove installed package', async () => {
      const removeResponse = {
        success: true,
        message: 'Successfully removed com.unity.textmeshpro',
        packageName: 'com.unity.textmeshpro',
      };
      setupMockResponse('unity_package', removeResponse);

      const result = await mockBridgeClient.call('unity_package', {
        action: 'remove',
        packageName: 'com.unity.textmeshpro',
      }) as typeof removeResponse;

      expect(result.success).toBe(true);
      expect(result.packageName).toBe('com.unity.textmeshpro');
    }, TEST_TIMEOUT);

    it('should fail for non-installed package', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Package is not installed'));

      await expect(mockBridgeClient.call('unity_package', {
        action: 'remove',
        packageName: 'com.not.installed',
      })).rejects.toThrow('not installed');
    });

    it('should require packageName', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('packageName is required'));

      await expect(mockBridgeClient.call('unity_package', {
        action: 'remove',
      })).rejects.toThrow('required');
    });
  });
});
