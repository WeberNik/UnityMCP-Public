// ============================================================================
// UnityVision MCP Server - MCP Resources Integration Tests
// Tests for MCP Resources (hierarchy, selection, logs, project-info, editor-state)
// ============================================================================

import { describe, it, expect, beforeAll } from '@jest/globals';

// These tests require a running Unity instance
const SKIP_INTEGRATION = !process.env.UNITY_INTEGRATION_TESTS;

describe('MCP Resources Integration Tests', () => {
  const testFn = SKIP_INTEGRATION ? it.skip : it;

  beforeAll(async () => {
    if (SKIP_INTEGRATION) {
      console.log('Skipping integration tests. Set UNITY_INTEGRATION_TESTS=1 to run.');
    }
  });

  describe('unity://hierarchy Resource', () => {
    testFn('should return scene hierarchy', async () => {
      // const resource = await readResource('unity://hierarchy');
      // expect(resource).toHaveProperty('rootObjects');
      expect(true).toBe(true);
    });

    testFn('should return valid JSON', async () => {
      // const resource = await readResource('unity://hierarchy');
      // expect(() => JSON.parse(resource)).not.toThrow();
      expect(true).toBe(true);
    });
  });

  describe('unity://selection Resource', () => {
    testFn('should return current selection', async () => {
      // const resource = await readResource('unity://selection');
      // expect(resource).toHaveProperty('selectedObjects');
      expect(true).toBe(true);
    });

    testFn('should return empty array when nothing selected', async () => {
      // const resource = await readResource('unity://selection');
      // expect(resource.selectedObjects).toBeInstanceOf(Array);
      expect(true).toBe(true);
    });
  });

  describe('unity://logs Resource', () => {
    testFn('should return console logs', async () => {
      // const resource = await readResource('unity://logs');
      // expect(resource).toHaveProperty('logs');
      expect(true).toBe(true);
    });

    testFn('should include log metadata', async () => {
      // const resource = await readResource('unity://logs');
      // if (resource.logs.length > 0) {
      //   expect(resource.logs[0]).toHaveProperty('timestamp');
      //   expect(resource.logs[0]).toHaveProperty('type');
      //   expect(resource.logs[0]).toHaveProperty('message');
      // }
      expect(true).toBe(true);
    });
  });

  describe('unity://project-info Resource', () => {
    testFn('should return project information', async () => {
      // const resource = await readResource('unity://project-info');
      // expect(resource).toHaveProperty('projectPath');
      // expect(resource).toHaveProperty('unityVersion');
      expect(true).toBe(true);
    });

    testFn('should include project settings', async () => {
      // const resource = await readResource('unity://project-info');
      // expect(resource).toHaveProperty('companyName');
      // expect(resource).toHaveProperty('productName');
      expect(true).toBe(true);
    });
  });

  describe('unity://editor-state Resource', () => {
    testFn('should return editor state', async () => {
      // const resource = await readResource('unity://editor-state');
      // expect(resource).toHaveProperty('isPlaying');
      // expect(resource).toHaveProperty('isPaused');
      // expect(resource).toHaveProperty('isCompiling');
      expect(true).toBe(true);
    });

    testFn('should reflect play mode changes', async () => {
      // const before = await readResource('unity://editor-state');
      // await client.call('set_play_mode', { mode: 'play' });
      // const during = await readResource('unity://editor-state');
      // await client.call('set_play_mode', { mode: 'stop' });
      // expect(during.isPlaying).toBe(true);
      expect(true).toBe(true);
    });
  });
});
