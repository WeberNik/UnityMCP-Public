#!/usr/bin/env node
// ============================================================================
// UnityVision MCP — Integration Test Suite v1.2.0
//
// Connects to the WebSocket hub on port 6400 and exercises connectivity,
// ping/pong, and hub status. For tool-level tests, use the MCP client
// (Windsurf/Claude) to call tools directly — this script validates the
// transport layer and new stability features.
//
// Prerequisites:
//   - Unity Editor open with the UnityVision bridge connected on port 6400
//   - MCP server running (via Windsurf or `node dist/server.js`)
//
// Run:  node tests/integration-test.mjs
// ============================================================================

import WebSocket from 'ws';
import { randomUUID } from 'crypto';
import { fileURLToPath } from 'url';

const WS_PORT = parseInt(process.env.UNITY_VISION_WS_PORT || '6400', 10);
const TIMEOUT_MS = 10000;

// ============================================================================
// Test Infrastructure
// ============================================================================

const results = [];

async function runTest(name, fn) {
  const start = performance.now();
  try {
    const result = await fn();
    const duration = performance.now() - start;
    results.push({ name, status: 'PASS', duration });
    console.log(`  PASS  ${name} (${duration.toFixed(0)}ms)`);
    return result;
  } catch (err) {
    const duration = performance.now() - start;
    const message = err?.message || String(err);
    results.push({ name, status: 'FAIL', duration, message });
    console.log(`  FAIL  ${name} -- ${message.slice(0, 150)} (${duration.toFixed(0)}ms)`);
    return null;
  }
}

function connectWs() {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(`ws://localhost:${WS_PORT}`);
    const timer = setTimeout(() => { ws.close(); reject(new Error('Connect timeout')); }, TIMEOUT_MS);
    ws.on('open', () => { clearTimeout(timer); resolve(ws); });
    ws.on('error', (e) => { clearTimeout(timer); reject(e); });
  });
}

function waitForMessage(ws, type, timeoutMs = TIMEOUT_MS) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(`Timeout waiting for ${type}`)), timeoutMs);
    const handler = (data) => {
      try {
        const msg = JSON.parse(data.toString());
        if (msg.type === type) {
          clearTimeout(timer);
          ws.removeListener('message', handler);
          resolve(msg);
        }
      } catch { /* ignore */ }
    };
    ws.on('message', handler);
  });
}

// ============================================================================
// Main
// ============================================================================

async function main() {
  console.log('================================================================');
  console.log('  UnityVision MCP -- Integration Test Suite v1.2.0');
  console.log('================================================================');
  console.log(`  Time: ${new Date().toISOString()}`);
  console.log(`  Port: ${WS_PORT}`);

  // --- 1. WebSocket Connectivity ---
  console.log('\n--- 1. WEBSOCKET CONNECTIVITY ---');

  let ws1 = null;
  await runTest('Connect to WebSocket hub', async () => {
    ws1 = await connectWs();
    return 'connected';
  });

  await runTest('Receive welcome message', async () => {
    const msg = await waitForMessage(ws1, 'welcome', 5000);
    console.log(`         serverTimeout=${msg.serverTimeout}s keepAlive=${msg.keepAliveInterval}s`);
    return msg;
  });

  // --- 2. Ping/Pong ---
  console.log('\n--- 2. PING/PONG HEALTH CHECK ---');

  await runTest('Send ping, receive pong', async () => {
    ws1.send(JSON.stringify({ type: 'ping' }));
    const msg = await waitForMessage(ws1, 'pong', 5000);
    return msg;
  });

  await runTest('Rapid ping x5', async () => {
    for (let i = 0; i < 5; i++) {
      ws1.send(JSON.stringify({ type: 'ping' }));
      await waitForMessage(ws1, 'pong', 3000);
    }
    return '5 pongs received';
  });

  // --- 3. Session Registration ---
  console.log('\n--- 3. SESSION REGISTRATION ---');

  await runTest('Register as test session', async () => {
    ws1.send(JSON.stringify({
      type: 'register',
      project_name: 'IntegrationTest',
      project_hash: 'test-' + randomUUID().slice(0, 8),
      unity_version: '2022.3.0f1-test',
      client_name: 'TestRunner',
      platform: 'TestPlatform',
    }));
    const msg = await waitForMessage(ws1, 'registered', 5000);
    console.log(`         session_id=${msg.session_id}`);
    return msg;
  });

  // --- 4. Multiple Connections ---
  console.log('\n--- 4. MULTIPLE CONNECTIONS ---');

  let ws2 = null;
  await runTest('Second WebSocket connects', async () => {
    ws2 = await connectWs();
    await waitForMessage(ws2, 'welcome', 5000);
    return 'connected';
  });

  await runTest('Second client ping/pong', async () => {
    ws2.send(JSON.stringify({ type: 'ping' }));
    await waitForMessage(ws2, 'pong', 3000);
    return 'pong received';
  });

  await runTest('Second client registers', async () => {
    ws2.send(JSON.stringify({
      type: 'register',
      project_name: 'IntegrationTest2',
      project_hash: 'test2-' + randomUUID().slice(0, 8),
      unity_version: '2022.3.0f1-test',
      client_name: 'TestRunner2',
      platform: 'TestPlatform',
    }));
    const msg = await waitForMessage(ws2, 'registered', 5000);
    return msg;
  });

  // --- 5. Disconnect Handling ---
  console.log('\n--- 5. DISCONNECT HANDLING ---');

  await runTest('Clean disconnect (ws2)', async () => {
    return new Promise((resolve) => {
      ws2.on('close', () => resolve('closed'));
      ws2.close();
    });
  });

  // --- 6. Reconnect ---
  console.log('\n--- 6. RECONNECT ---');

  await runTest('Reconnect after disconnect', async () => {
    ws2 = await connectWs();
    await waitForMessage(ws2, 'welcome', 5000);
    ws2.send(JSON.stringify({ type: 'ping' }));
    await waitForMessage(ws2, 'pong', 3000);
    ws2.close();
    return 'reconnected and pinged';
  });

  // --- 7. Stress: Rapid connect/disconnect ---
  console.log('\n--- 7. STRESS: RAPID CONNECT/DISCONNECT ---');

  await runTest('10x rapid connect/ping/disconnect', async () => {
    const times = [];
    for (let i = 0; i < 10; i++) {
      const s = performance.now();
      const ws = await connectWs();
      await waitForMessage(ws, 'welcome', 5000);
      ws.send(JSON.stringify({ type: 'ping' }));
      await waitForMessage(ws, 'pong', 3000);
      ws.close();
      times.push(performance.now() - s);
    }
    const avg = times.reduce((a, b) => a + b, 0) / times.length;
    console.log(`         avg=${avg.toFixed(0)}ms min=${Math.min(...times).toFixed(0)}ms max=${Math.max(...times).toFixed(0)}ms`);
    return { avg: Math.round(avg), times: times.map(t => Math.round(t)) };
  });

  // --- 8. Message Throughput ---
  console.log('\n--- 8. MESSAGE THROUGHPUT ---');

  await runTest('50x rapid pings on single connection', async () => {
    const start = performance.now();
    let received = 0;
    const ws = await connectWs();
    await waitForMessage(ws, 'welcome', 5000);

    await new Promise((resolve, reject) => {
      const timer = setTimeout(() => reject(new Error(`Only received ${received}/50 pongs`)), 15000);
      ws.on('message', (data) => {
        try {
          const msg = JSON.parse(data.toString());
          if (msg.type === 'pong') {
            received++;
            if (received >= 50) { clearTimeout(timer); resolve(); }
          }
        } catch { /* ignore */ }
      });
      for (let i = 0; i < 50; i++) {
        ws.send(JSON.stringify({ type: 'ping' }));
      }
    });

    ws.close();
    const total = performance.now() - start;
    console.log(`         50 pings in ${total.toFixed(0)}ms (${(50000 / total).toFixed(0)} msg/s)`);
    return { totalMs: Math.round(total), msgsPerSec: Math.round(50000 / total) };
  });

  // Cleanup
  if (ws1) ws1.close();

  // ================================================================
  // SUMMARY
  // ================================================================

  console.log('\n================================================================');
  console.log('                    TEST RESULTS SUMMARY');
  console.log('================================================================');

  const passed = results.filter(r => r.status === 'PASS');
  const failed = results.filter(r => r.status === 'FAIL');

  console.log(`  PASS:    ${passed.length}`);
  console.log(`  FAIL:    ${failed.length}`);
  console.log(`  TOTAL:   ${results.length}`);

  if (passed.length > 0) {
    const durations = passed.map(r => r.duration);
    const avg = durations.reduce((a, b) => a + b, 0) / durations.length;
    const total = durations.reduce((a, b) => a + b, 0);
    console.log(`\n  Performance (passing tests):`);
    console.log(`     Average: ${avg.toFixed(0)}ms`);
    console.log(`     Min:     ${Math.min(...durations).toFixed(0)}ms`);
    console.log(`     Max:     ${Math.max(...durations).toFixed(0)}ms`);
    console.log(`     Total:   ${(total / 1000).toFixed(1)}s`);
  }

  if (failed.length > 0) {
    console.log('\n  -- Failed Tests --');
    for (const f of failed) {
      console.log(`  FAIL  ${f.name}: ${f.message}`);
    }
  }

  console.log(`\n  ${failed.length === 0 ? 'All tests passed!' : `${failed.length} test(s) failed.`}`);
  process.exit(failed.length > 0 ? 1 : 0);
}

main().catch(err => {
  console.error('Fatal:', err);
  process.exit(1);
});
