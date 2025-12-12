// ============================================================================
// UnityVision MCP Server - Script Tools Tests
// Tests for unity_script tool (create, read, update, delete, validate, get_sha)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, mockResponses, setupMockResponse, resetMocks, testData, TEST_TIMEOUT } from '../setup.js';


describe('unity_script Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('create action', () => {
    it('should create a new MonoBehaviour script', async () => {
      const createResponse = {
        success: true,
        path: 'Assets/Scripts/PlayerController.cs',
        className: 'PlayerController',
        sha256: 'abc123',
      };
      setupMockResponse('unity_script', createResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'create',
        path: 'Assets/Scripts/PlayerController.cs',
        template: 'MonoBehaviour',
      }) as typeof createResponse;

      expect(result.success).toBe(true);
      expect(result.path).toContain('PlayerController.cs');
      expect(result.className).toBe('PlayerController');
    }, TEST_TIMEOUT);

    it('should create a ScriptableObject script', async () => {
      const createResponse = {
        success: true,
        path: 'Assets/Scripts/GameSettings.cs',
        className: 'GameSettings',
        sha256: 'def456',
      };
      setupMockResponse('unity_script', createResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'create',
        path: 'Assets/Scripts/GameSettings.cs',
        template: 'ScriptableObject',
      }) as typeof createResponse;

      expect(result.success).toBe(true);
      expect(result.className).toBe('GameSettings');
    });

    it('should create an Editor script', async () => {
      const createResponse = {
        success: true,
        path: 'Assets/Editor/CustomInspector.cs',
        className: 'CustomInspector',
        sha256: 'ghi789',
      };
      setupMockResponse('unity_script', createResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'create',
        path: 'Assets/Editor/CustomInspector.cs',
        template: 'Editor',
      }) as typeof createResponse;

      expect(result.success).toBe(true);
      expect(result.path).toContain('Editor/');
    });

    it('should reject invalid path', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Path must be within Assets folder'));

      await expect(mockBridgeClient.call('unity_script', {
        action: 'create',
        path: '../outside/script.cs',
        template: 'MonoBehaviour',
      })).rejects.toThrow('Assets folder');
    });

    it('should reject path without .cs extension', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Path must end with .cs'));

      await expect(mockBridgeClient.call('unity_script', {
        action: 'create',
        path: 'Assets/Scripts/NoExtension',
        template: 'MonoBehaviour',
      })).rejects.toThrow('.cs');
    });
  });

  describe('read action', () => {
    it('should read script contents', async () => {
      const readResponse = {
        success: true,
        path: 'Assets/Scripts/TestScript.cs',
        contents: testData.sampleScript,
        sha256: 'abc123',
        sizeBytes: testData.sampleScript.length,
      };
      setupMockResponse('unity_script', readResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'read',
        path: 'Assets/Scripts/TestScript.cs',
      }) as typeof readResponse;

      expect(result.success).toBe(true);
      expect(result.contents).toContain('MonoBehaviour');
      expect(result.sha256).toBeDefined();
    });

    it('should return file metadata', async () => {
      const readResponse = {
        success: true,
        path: 'Assets/Scripts/TestScript.cs',
        contents: testData.sampleScript,
        sha256: 'abc123',
        sizeBytes: 256,
        lastModified: '2025-12-07T15:00:00Z',
      };
      setupMockResponse('unity_script', readResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'read',
        path: 'Assets/Scripts/TestScript.cs',
      }) as typeof readResponse;

      expect(result.sizeBytes).toBeGreaterThan(0);
    });

    it('should fail for non-existent script', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Script not found'));

      await expect(mockBridgeClient.call('unity_script', {
        action: 'read',
        path: 'Assets/Scripts/NonExistent.cs',
      })).rejects.toThrow('not found');
    });
  });

  describe('update action', () => {
    it('should update script contents', async () => {
      const updateResponse = {
        success: true,
        path: 'Assets/Scripts/TestScript.cs',
        sha256: 'newsha456',
      };
      setupMockResponse('unity_script', updateResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'update',
        path: 'Assets/Scripts/TestScript.cs',
        contents: testData.sampleMonoBehaviour('UpdatedScript'),
      }) as typeof updateResponse;

      expect(result.success).toBe(true);
      expect(result.sha256).toBeDefined();
    });

    it('should support base64 encoded contents', async () => {
      const updateResponse = {
        success: true,
        path: 'Assets/Scripts/TestScript.cs',
        sha256: 'base64sha789',
      };
      setupMockResponse('unity_script', updateResponse);

      const base64Content = Buffer.from(testData.sampleScript).toString('base64');
      const result = await mockBridgeClient.call('unity_script', {
        action: 'update',
        path: 'Assets/Scripts/TestScript.cs',
        contents: base64Content,
        isBase64: true,
      }) as typeof updateResponse;

      expect(result.success).toBe(true);
    });

    it('should detect conflicts with expectedSha', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('SHA256 mismatch: file was modified'));

      await expect(mockBridgeClient.call('unity_script', {
        action: 'update',
        path: 'Assets/Scripts/TestScript.cs',
        contents: 'new content',
        expectedSha: 'wrongsha',
      })).rejects.toThrow('SHA256 mismatch');
    });
  });

  describe('delete action', () => {
    it('should delete script and meta file', async () => {
      const deleteResponse = {
        success: true,
        path: 'Assets/Scripts/ToDelete.cs',
        metaDeleted: true,
      };
      setupMockResponse('unity_script', deleteResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'delete',
        path: 'Assets/Scripts/ToDelete.cs',
      }) as typeof deleteResponse;

      expect(result.success).toBe(true);
      expect(result.metaDeleted).toBe(true);
    });

    it('should fail for non-existent script', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Script not found'));

      await expect(mockBridgeClient.call('unity_script', {
        action: 'delete',
        path: 'Assets/Scripts/NonExistent.cs',
      })).rejects.toThrow('not found');
    });
  });

  describe('validate action', () => {
    it('should validate correct script syntax', async () => {
      const validateResponse = {
        success: true,
        isValid: true,
        errors: [],
        warnings: [],
      };
      setupMockResponse('unity_script', validateResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'validate',
        path: 'Assets/Scripts/ValidScript.cs',
      }) as typeof validateResponse;

      expect(result.isValid).toBe(true);
      expect(result.errors).toHaveLength(0);
    });

    it('should detect unbalanced braces', async () => {
      const validateResponse = {
        success: true,
        isValid: false,
        errors: ['Unbalanced braces: 3 open, 2 close'],
        warnings: [],
      };
      setupMockResponse('unity_script', validateResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'validate',
        path: 'Assets/Scripts/InvalidScript.cs',
      }) as typeof validateResponse;

      expect(result.isValid).toBe(false);
      expect(result.errors.length).toBeGreaterThan(0);
    });

    it('should detect class name mismatch', async () => {
      const validateResponse = {
        success: true,
        isValid: false,
        errors: [],
        warnings: ['Class name "WrongName" does not match filename "CorrectName.cs"'],
      };
      setupMockResponse('unity_script', validateResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'validate',
        path: 'Assets/Scripts/CorrectName.cs',
        level: 'standard',
      }) as typeof validateResponse;

      expect(result.warnings.length).toBeGreaterThan(0);
    });
  });

  describe('get_sha action', () => {
    it('should return SHA256 hash', async () => {
      const shaResponse = {
        success: true,
        path: 'Assets/Scripts/TestScript.cs',
        sha256: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
      };
      setupMockResponse('unity_script', shaResponse);

      const result = await mockBridgeClient.call('unity_script', {
        action: 'get_sha',
        path: 'Assets/Scripts/TestScript.cs',
      }) as typeof shaResponse;

      expect(result.sha256).toMatch(/^[a-f0-9]{64}$/);
    });
  });
});
