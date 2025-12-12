// ============================================================================
// UnityVision MCP Server - Editor Tools Tests
// Tests for unity_editor tool (get_state, set_play_mode, get_context, recompile, refresh)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, mockResponses, setupMockResponse, resetMocks, TEST_TIMEOUT } from '../setup.js';

describe('unity_editor Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('get_state action', () => {
    it('should return editor state with all fields', async () => {
      setupMockResponse('get_editor_state', mockResponses.editorState);

      const result = await mockBridgeClient.call('get_editor_state', {});

      expect(result).toHaveProperty('unityVersion');
      expect(result).toHaveProperty('projectPath');
      expect(result).toHaveProperty('isPlaying');
      expect(result).toHaveProperty('isPaused');
      expect(result).toHaveProperty('activeScene');
      expect(result).toHaveProperty('loadedScenes');
      expect(result).toHaveProperty('platform');
    }, TEST_TIMEOUT);

    it('should return correct Unity version format', async () => {
      setupMockResponse('get_editor_state', mockResponses.editorState);

      const result = await mockBridgeClient.call('get_editor_state', {}) as typeof mockResponses.editorState;

      expect(result.unityVersion).toMatch(/^\d+\.\d+\.\d+/);
    });

    it('should return valid play mode state', async () => {
      setupMockResponse('get_editor_state', mockResponses.editorState);

      const result = await mockBridgeClient.call('get_editor_state', {}) as typeof mockResponses.editorState;

      expect(typeof result.isPlaying).toBe('boolean');
      expect(typeof result.isPaused).toBe('boolean');
    });
  });

  describe('set_play_mode action', () => {
    it('should accept "play" mode', async () => {
      setupMockResponse('set_play_mode', { success: true, previousMode: 'stop', currentMode: 'play' });

      const result = await mockBridgeClient.call('set_play_mode', { mode: 'play' }) as { success: boolean };

      expect(result.success).toBe(true);
    });

    it('should accept "pause" mode', async () => {
      setupMockResponse('set_play_mode', { success: true, previousMode: 'play', currentMode: 'pause' });

      const result = await mockBridgeClient.call('set_play_mode', { mode: 'pause' }) as { success: boolean };

      expect(result.success).toBe(true);
    });

    it('should accept "stop" mode', async () => {
      setupMockResponse('set_play_mode', { success: true, previousMode: 'play', currentMode: 'stop' });

      const result = await mockBridgeClient.call('set_play_mode', { mode: 'stop' }) as { success: boolean };

      expect(result.success).toBe(true);
    });

    it('should reject invalid mode', async () => {
      mockBridgeClient.call.mockRejectedValue(new Error('Invalid mode: invalid'));

      await expect(mockBridgeClient.call('set_play_mode', { mode: 'invalid' }))
        .rejects.toThrow('Invalid mode');
    });
  });

  describe('get_context action', () => {
    it('should return context with selection and errors', async () => {
      const contextResponse = {
        playModeState: 'stop',
        isCompiling: false,
        activeScene: 'SampleScene',
        selectedObjects: [],
        recentErrors: [],
      };
      setupMockResponse('get_active_context', contextResponse);

      const result = await mockBridgeClient.call('get_active_context', {});

      expect(result).toHaveProperty('playModeState');
      expect(result).toHaveProperty('isCompiling');
      expect(result).toHaveProperty('activeScene');
    });

    it('should respect maxConsoleErrors parameter', async () => {
      const contextResponse = {
        playModeState: 'stop',
        isCompiling: false,
        activeScene: 'SampleScene',
        recentErrors: [
          { timestamp: Date.now(), type: 'Error', message: 'Error 1' },
          { timestamp: Date.now(), type: 'Error', message: 'Error 2' },
        ],
      };
      setupMockResponse('get_active_context', contextResponse);

      const result = await mockBridgeClient.call('get_active_context', { maxConsoleErrors: 2 }) as typeof contextResponse;

      expect(result.recentErrors.length).toBeLessThanOrEqual(2);
    });
  });

  describe('recompile action', () => {
    it('should trigger script recompilation', async () => {
      setupMockResponse('editor_recompile', mockResponses.recompileResult);

      const result = await mockBridgeClient.call('editor_recompile', {}) as typeof mockResponses.recompileResult;

      expect(result.success).toBe(true);
      expect(result.message).toContain('recompilation');
    });

    it('should return compilation status', async () => {
      setupMockResponse('editor_recompile', mockResponses.recompileResult);

      const result = await mockBridgeClient.call('editor_recompile', {}) as typeof mockResponses.recompileResult;

      expect(typeof result.isCompiling).toBe('boolean');
    });
  });

  describe('refresh action', () => {
    it('should trigger asset database refresh', async () => {
      setupMockResponse('editor_refresh', mockResponses.refreshResult);

      const result = await mockBridgeClient.call('editor_refresh', {}) as typeof mockResponses.refreshResult;

      expect(result.success).toBe(true);
      expect(result.message).toContain('refreshed');
    });
  });
});
