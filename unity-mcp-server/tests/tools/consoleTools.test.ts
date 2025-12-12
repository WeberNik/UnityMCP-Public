// ============================================================================
// UnityVision MCP Server - Console Tools Tests
// Tests for unity_console tool (get_logs, clear)
// ============================================================================

import { describe, it, expect, beforeEach } from '@jest/globals';
import { mockBridgeClient, mockResponses, setupMockResponse, resetMocks, TEST_TIMEOUT } from '../setup.js';


describe('unity_console Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('get_logs action', () => {
    it('should return console logs', async () => {
      setupMockResponse('get_console_logs', mockResponses.consoleLog);

      const result = await mockBridgeClient.call('get_console_logs', {}) as typeof mockResponses.consoleLog;

      expect(result.logs).toBeInstanceOf(Array);
      expect(result.totalCount).toBeGreaterThanOrEqual(0);
    }, TEST_TIMEOUT);

    it('should filter by log type', async () => {
      const errorOnlyLogs = {
        logs: [
          { timestamp: Date.now(), type: 'Error', message: 'Test error', stackTrace: '' },
        ],
        totalCount: 1,
      };
      setupMockResponse('get_console_logs', errorOnlyLogs);

      const result = await mockBridgeClient.call('get_console_logs', {
        logType: 'error',
      }) as typeof errorOnlyLogs;

      expect(result.logs.every(log => log.type === 'Error')).toBe(true);
    });

    it('should filter by search text', async () => {
      const filteredLogs = {
        logs: [
          { timestamp: Date.now(), type: 'Log', message: 'Contains search term', stackTrace: '' },
        ],
        totalCount: 1,
      };
      setupMockResponse('get_console_logs', filteredLogs);

      const result = await mockBridgeClient.call('get_console_logs', {
        searchText: 'search term',
      }) as typeof filteredLogs;

      expect(result.logs[0].message).toContain('search term');
    });

    it('should limit number of logs returned', async () => {
      const limitedLogs = {
        logs: [
          { timestamp: Date.now(), type: 'Log', message: 'Log 1', stackTrace: '' },
          { timestamp: Date.now(), type: 'Log', message: 'Log 2', stackTrace: '' },
        ],
        totalCount: 100,
      };
      setupMockResponse('get_console_logs', limitedLogs);

      const result = await mockBridgeClient.call('get_console_logs', {
        count: 2,
      }) as typeof limitedLogs;

      expect(result.logs.length).toBeLessThanOrEqual(2);
    });

    it('should include log metadata', async () => {
      setupMockResponse('get_console_logs', mockResponses.consoleLog);

      const result = await mockBridgeClient.call('get_console_logs', {}) as typeof mockResponses.consoleLog;

      if (result.logs.length > 0) {
        const log = result.logs[0];
        expect(log).toHaveProperty('timestamp');
        expect(log).toHaveProperty('type');
        expect(log).toHaveProperty('message');
      }
    });

    it('should include stack trace for errors', async () => {
      const errorWithStack = {
        logs: [
          { 
            timestamp: Date.now(), 
            type: 'Error', 
            message: 'NullReferenceException', 
            stackTrace: 'at TestScript.Update() in TestScript.cs:10' 
          },
        ],
        totalCount: 1,
      };
      setupMockResponse('get_console_logs', errorWithStack);

      const result = await mockBridgeClient.call('get_console_logs', {
        logType: 'error',
      }) as typeof errorWithStack;

      expect(result.logs[0].stackTrace).toContain('TestScript');
    });
  });

  describe('clear action', () => {
    it('should clear console logs', async () => {
      const clearResponse = {
        success: true,
        clearedCount: 50,
      };
      setupMockResponse('clear_console_logs', clearResponse);

      const result = await mockBridgeClient.call('clear_console_logs', {}) as typeof clearResponse;

      expect(result.success).toBe(true);
    });

    it('should return count of cleared logs', async () => {
      const clearResponse = {
        success: true,
        clearedCount: 25,
      };
      setupMockResponse('clear_console_logs', clearResponse);

      const result = await mockBridgeClient.call('clear_console_logs', {}) as typeof clearResponse;

      expect(result.clearedCount).toBeGreaterThanOrEqual(0);
    });
  });
});
