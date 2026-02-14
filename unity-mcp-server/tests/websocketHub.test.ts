import { describe, it, expect, beforeEach, afterEach, jest } from '@jest/globals';
import { WebSocketPluginHub } from '../src/websocketHub.js';

type AnyHub = any;

function newHub(): AnyHub {
  (WebSocketPluginHub as any).instance = null;
  return WebSocketPluginHub.getInstance(6400) as AnyHub;
}

function fakeSocket() {
  return {
    send: jest.fn(),
    close: jest.fn(),
  };
}

describe('WebSocketPluginHub', () => {
  let hub: AnyHub;

  beforeEach(() => {
    hub = newHub();
  });

  afterEach(() => {
    if (hub?.keepAliveTimer) {
      clearInterval(hub.keepAliveTimer);
      hub.keepAliveTimer = null;
    }
    jest.useRealTimers();
  });

  it('rejects command_result with top-level error shape', () => {
    const reject = jest.fn();
    const resolve = jest.fn();
    const timeout = setTimeout(() => {}, 1000);

    hub.pendingCommands.set('cmd-1', {
      id: 'cmd-1',
      method: 'get_editor_state',
      sessionId: 's1',
      resolve,
      reject,
      timeout,
      startTime: Date.now() - 25,
    });

    hub.handleCommandResult({
      type: 'command_result',
      id: 'cmd-1',
      error: { code: 'UNITY_BUSY', message: 'Unity is compiling' },
    });

    expect(reject).toHaveBeenCalledTimes(1);
    expect(String(reject.mock.calls[0][0])).toContain('Unity is compiling');
    expect(resolve).not.toHaveBeenCalled();
    expect(hub.pendingCommands.has('cmd-1')).toBe(false);
  });

  it('rejects pending commands for disconnected session immediately', () => {
    const socketA = fakeSocket();
    const socketB = fakeSocket();
    const rejectA = jest.fn();
    const rejectB = jest.fn();

    hub.sessions.set('session-a', {
      sessionId: 'session-a',
      projectName: 'A',
      projectHash: 'hash-a',
      unityVersion: '2022.3',
      connectedAt: new Date(),
      lastPing: new Date(),
      socket: socketA,
      customTools: new Map(),
    });
    hub.sessions.set('session-b', {
      sessionId: 'session-b',
      projectName: 'B',
      projectHash: 'hash-b',
      unityVersion: '2022.3',
      connectedAt: new Date(),
      lastPing: new Date(),
      socket: socketB,
      customTools: new Map(),
    });

    hub.pendingCommands.set('c-a', {
      id: 'c-a',
      method: 'foo',
      sessionId: 'session-a',
      resolve: jest.fn(),
      reject: rejectA,
      timeout: setTimeout(() => {}, 1000),
      startTime: Date.now(),
    });
    hub.pendingCommands.set('c-b', {
      id: 'c-b',
      method: 'bar',
      sessionId: 'session-b',
      resolve: jest.fn(),
      reject: rejectB,
      timeout: setTimeout(() => {}, 1000),
      startTime: Date.now(),
    });

    hub.handleDisconnect(socketA, 1006, 'lost');

    expect(rejectA).toHaveBeenCalledTimes(1);
    expect(rejectB).not.toHaveBeenCalled();
    expect(hub.pendingCommands.has('c-a')).toBe(false);
    expect(hub.pendingCommands.has('c-b')).toBe(true);
    expect(hub.sessions.has('session-a')).toBe(false);
    expect(hub.sessions.has('session-b')).toBe(true);
  });

  it('heartbeats out dead sessions and cleans them up', () => {
    jest.useFakeTimers();

    const oldSessionSocket = fakeSocket();
    hub.sessions.set('dead', {
      sessionId: 'dead',
      projectName: 'DeadProject',
      projectHash: 'dead-hash',
      unityVersion: '2022.3',
      connectedAt: new Date(Date.now() - 60000),
      lastPing: new Date(Date.now() - 60000),
      socket: oldSessionSocket,
      customTools: new Map(),
    });

    hub.startHeartbeat();
    jest.advanceTimersByTime(hub.KEEP_ALIVE_INTERVAL + 5);

    expect(oldSessionSocket.close).toHaveBeenCalled();
    expect(hub.sessions.has('dead')).toBe(false);
  });

  it('opens circuit after threshold timeouts and resets on success', async () => {
    jest.useFakeTimers();

    const socket = fakeSocket();
    hub.sessions.set('s1', {
      sessionId: 's1',
      projectName: 'Test',
      projectHash: 'hash',
      unityVersion: '2022.3',
      connectedAt: new Date(),
      lastPing: new Date(),
      socket,
      customTools: new Map(),
    });

    hub.COMMAND_TIMEOUT = 10;
    hub.CIRCUIT_BREAKER_THRESHOLD = 3;
    hub.CIRCUIT_BREAKER_COOLDOWN = 2000;

    const p1 = hub.sendCommand('m1', {});
    const p1Assert = expect(p1).rejects.toThrow('timed out');
    await Promise.resolve();
    await jest.advanceTimersByTimeAsync(20);
    await p1Assert;

    const p2 = hub.sendCommand('m2', {});
    const p2Assert = expect(p2).rejects.toThrow('timed out');
    await Promise.resolve();
    await jest.advanceTimersByTimeAsync(20);
    await p2Assert;

    const p3 = hub.sendCommand('m3', {});
    const p3Assert = expect(p3).rejects.toThrow('timed out');
    await Promise.resolve();
    await jest.advanceTimersByTimeAsync(20);
    await p3Assert;

    expect(hub.circuitBreakerUntil).toBeGreaterThan(Date.now());
    await expect(hub.sendCommand('blocked', {})).rejects.toThrow('Circuit breaker open');

    const timeout = setTimeout(() => {}, 1000);
    const resolve = jest.fn();
    const reject = jest.fn();
    hub.pendingCommands.set('ok', {
      id: 'ok',
      method: 'ok',
      sessionId: 's1',
      resolve,
      reject,
      timeout,
      startTime: Date.now(),
    });
    hub.consecutiveFailures = 2;
    hub.circuitBreakerUntil = Date.now() + 9999;

    hub.handleCommandResult({
      type: 'command_result',
      id: 'ok',
      result: { status: 'success', value: true },
    });

    expect(resolve).toHaveBeenCalled();
    expect(reject).not.toHaveBeenCalled();
    expect(hub.consecutiveFailures).toBe(0);
    expect(hub.circuitBreakerUntil).toBe(0);
  });
});
