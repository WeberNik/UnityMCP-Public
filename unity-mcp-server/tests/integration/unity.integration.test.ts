/**
 * UnityVision Integration Tests
 * 
 * These tests require a running Unity instance with the UnityVision package installed.
 * Run with: UNITY_INTEGRATION_TESTS=1 npm run test:integration
 * 
 * Prerequisites:
 * 1. Unity Editor running with UnityVision package
 * 2. WebSocket connection established (check Bridge Status window)
 * 3. A test scene loaded
 */

import { describe, it, expect, beforeAll, afterAll } from '@jest/globals';
import { WebSocketPluginHub, startWebSocketHub } from '../../src/websocketHub.js';

// Skip all tests if not running integration tests
const SKIP_INTEGRATION = !process.env.UNITY_INTEGRATION_TESTS;

describe('Unity Integration Tests', () => {
  let hub: WebSocketPluginHub;
  
  beforeAll(async () => {
    if (SKIP_INTEGRATION) return;
    
    // Start WebSocket hub
    hub = await startWebSocketHub(7890);
    
    // Wait for Unity to connect (up to 10 seconds)
    const maxWait = 10000;
    const checkInterval = 500;
    let waited = 0;
    
    while (!hub.isConnected() && waited < maxWait) {
      await new Promise(resolve => setTimeout(resolve, checkInterval));
      waited += checkInterval;
    }
    
    if (!hub.isConnected()) {
      console.warn('Unity not connected - integration tests will be skipped');
    }
  });
  
  afterAll(() => {
    if (hub) {
      hub.stop();
    }
  });
  
  // Helper to skip test if Unity not connected
  const itIfConnected = (name: string, fn: () => Promise<void>) => {
    it(name, async () => {
      if (SKIP_INTEGRATION) {
        console.log(`Skipping: ${name} (UNITY_INTEGRATION_TESTS not set)`);
        return;
      }
      if (!hub?.isConnected()) {
        console.log(`Skipping: ${name} (Unity not connected)`);
        return;
      }
      await fn();
    });
  };
  
  describe('Connection', () => {
    itIfConnected('should have Unity connected', async () => {
      expect(hub.isConnected()).toBe(true);
      expect(hub.getSessionCount()).toBeGreaterThan(0);
    });
    
    itIfConnected('should have session info', async () => {
      const sessions = hub.getSessions();
      expect(sessions.length).toBeGreaterThan(0);
      
      const session = sessions[0];
      expect(session.projectName).toBeDefined();
      expect(session.unityVersion).toBeDefined();
      expect(session.sessionId).toBeDefined();
    });
  });
  
  describe('Editor State', () => {
    itIfConnected('should get editor state', async () => {
      const result = await hub.sendCommand<any>('get_editor_state', {});
      
      expect(result).toBeDefined();
      expect(result.unityVersion).toBeDefined();
      expect(result.projectPath).toBeDefined();
      expect(typeof result.isPlaying).toBe('boolean');
    });
    
    itIfConnected('should get active context', async () => {
      const result = await hub.sendCommand<any>('get_active_context', {
        maxConsoleErrors: 5,
        includeSelection: true,
        includePlayModeState: true
      });
      
      expect(result).toBeDefined();
      expect(result.playModeState).toBeDefined();
      expect(typeof result.isCompiling).toBe('boolean');
    });
  });
  
  describe('Scene Operations', () => {
    itIfConnected('should list scenes', async () => {
      const result = await hub.sendCommand<any>('list_scenes', { filter: '' });
      
      expect(result).toBeDefined();
      expect(Array.isArray(result.scenes)).toBe(true);
    });
    
    itIfConnected('should get scene hierarchy', async () => {
      const result = await hub.sendCommand<any>('get_scene_hierarchy', {
        maxDepth: 3,
        includeComponents: false,
        maxObjects: 100
      });
      
      expect(result).toBeDefined();
      expect(Array.isArray(result.rootObjects)).toBe(true);
      expect(typeof result.totalObjectsInScene).toBe('number');
    });
  });
  
  describe('Console Operations', () => {
    itIfConnected('should get console logs', async () => {
      const result = await hub.sendCommand<any>('get_console_logs', {
        sinceTimeMs: 0,
        maxEntries: 10,
        level: 'all',
        includeStackTrace: false
      });
      
      expect(result).toBeDefined();
      expect(Array.isArray(result.entries)).toBe(true);
    });
  });
  
  describe('GameObject Operations', () => {
    let testObjectId: string | null = null;
    
    itIfConnected('should create a GameObject', async () => {
      const result = await hub.sendCommand<any>('create_game_object', {
        name: 'IntegrationTestObject',
        primitiveType: 'Cube',
        position: { x: 0, y: 0, z: 0 }
      });
      
      expect(result).toBeDefined();
      expect(result.success).toBe(true);
      expect(result.id).toBeDefined();
      
      testObjectId = result.id;
    });
    
    itIfConnected('should delete the test GameObject', async () => {
      if (!testObjectId) {
        console.log('Skipping delete - no test object created');
        return;
      }
      
      const result = await hub.sendCommand<any>('delete_game_object', {
        path: testObjectId,
        confirm: true
      });
      
      expect(result).toBeDefined();
      expect(result.success).toBe(true);
    });
  });
  
  describe('Screenshot Operations', () => {
    itIfConnected('should capture game view screenshot', async () => {
      const result = await hub.sendCommand<any>('capture_game_view_screenshot', {
        width: 256,
        height: 256
      });
      
      expect(result).toBeDefined();
      expect(result.imageData).toBeDefined();
      expect(result.width).toBe(256);
      expect(result.height).toBe(256);
    });
  });
  
  describe('Rate Limiting', () => {
    itIfConnected('should handle rapid requests without error', async () => {
      // Send 10 rapid requests
      const promises = [];
      for (let i = 0; i < 10; i++) {
        promises.push(hub.sendCommand<any>('get_editor_state', {}));
      }
      
      const results = await Promise.all(promises);
      
      // All should succeed (under rate limit)
      for (const result of results) {
        expect(result).toBeDefined();
        expect(result.unityVersion).toBeDefined();
      }
    });
  });
});

// Export for use in other test files
export { SKIP_INTEGRATION };
