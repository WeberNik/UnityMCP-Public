#!/usr/bin/env node
// ============================================================================
// UnityVision MCP — Tool-Level Test Suite v1.2.0
//
// Starts its own WebSocket hub on an alternate port and tests:
//   1. Hub lifecycle (start/stop)
//   2. Tool error handling (graceful degradation when no Unity)
//   3. Status tools (work without Unity)
//   4. Tool registry completeness
//
// Run:  node tests/tool-level-test.mjs
// ============================================================================

import { fileURLToPath } from 'url';
import path from 'path';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const TEST_PORT = 6499; // Different from production port 6400

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

async function main() {
  console.log('================================================================');
  console.log('  UnityVision MCP -- Tool-Level Test Suite v1.2.0');
  console.log('================================================================');
  console.log(`  Time: ${new Date().toISOString()}`);
  console.log(`  Test Port: ${TEST_PORT}`);

  // Import built modules
  const { startWebSocketHub, getWebSocketHub } = await import('../dist/websocketHub.js');
  const { executeTool } = await import('../dist/tools/index.js');
  const { allToolDefinitions } = await import('../dist/tools/index.js');

  // --- 1. Hub Lifecycle ---
  console.log('\n--- 1. HUB LIFECYCLE ---');

  let hub = null;
  await runTest('Start hub on alternate port', async () => {
    hub = await startWebSocketHub(TEST_PORT);
    const status = hub.getStatus();
    if (!status.running) throw new Error('Hub not running');
    console.log(`         Port: ${status.port}, Running: ${status.running}`);
    return status;
  });

  await runTest('Hub getStatus returns valid data', async () => {
    const status = hub.getStatus();
    if (typeof status.running !== 'boolean') throw new Error('Missing running field');
    if (typeof status.port !== 'number') throw new Error('Missing port field');
    if (!Array.isArray(status.sessions)) throw new Error('Missing sessions array');
    console.log(`         Sessions: ${status.sessions.length}, Pending: ${status.pendingCommands?.count || 0}`);
    return status;
  });

  await runTest('Hub reports circuit breaker status', async () => {
    const status = hub.getStatus();
    if (status.circuitBreaker === undefined) throw new Error('Missing circuitBreaker field');
    console.log(`         Circuit breaker: ${JSON.stringify(status.circuitBreaker)}`);
    return status.circuitBreaker;
  });

  // --- 2. Tool Registry ---
  console.log('\n--- 2. TOOL REGISTRY ---');

  await runTest('Tool definitions loaded', async () => {
    if (!allToolDefinitions || allToolDefinitions.length === 0) throw new Error('No tool definitions');
    console.log(`         ${allToolDefinitions.length} tools registered`);
    return allToolDefinitions.length;
  });

  await runTest('All tools have name and description', async () => {
    for (const tool of allToolDefinitions) {
      if (!tool.name) throw new Error(`Tool missing name: ${JSON.stringify(tool)}`);
      if (!tool.description) throw new Error(`Tool ${tool.name} missing description`);
    }
    return 'all valid';
  });

  await runTest('All tools have input schema', async () => {
    for (const tool of allToolDefinitions) {
      if (!tool.inputSchema) throw new Error(`Tool ${tool.name} missing inputSchema`);
    }
    return 'all valid';
  });

  const expectedTools = [
    'unity_editor', 'unity_console', 'unity_scene', 'unity_gameobject',
    'unity_component', 'unity_selection', 'unity_query', 'unity_asset',
    'unity_material', 'unity_profiler', 'unity_screenshot', 'unity_code',
    'unity_menu', 'unity_audio', 'unity_animation', 'unity_ui',
    'unity_project', 'unity_status', 'unity_package', 'unity_dependency',
  ];

  await runTest('Expected core tools present', async () => {
    const toolNames = allToolDefinitions.map(t => t.name);
    const missing = expectedTools.filter(t => !toolNames.includes(t));
    if (missing.length > 0) throw new Error(`Missing tools: ${missing.join(', ')}`);
    console.log(`         All ${expectedTools.length} core tools present`);
    return expectedTools.length;
  });

  // --- 3. Graceful Error Handling (No Unity Connected) ---
  console.log('\n--- 3. GRACEFUL ERROR HANDLING (No Unity) ---');

  const toolsToTest = [
    ['unity_editor', { action: 'get_state' }],
    ['unity_console', { action: 'get_logs' }],
    ['unity_scene', { action: 'list' }],
    ['unity_selection', { action: 'get' }],
    ['unity_profiler', { action: 'rendering_stats' }],
    ['unity_code', { action: 'evaluate', expression: '2+2' }],
    ['unity_gameobject', { action: 'create', name: 'Test', primitiveType: 'Cube' }],
  ];

  for (const [toolName, args] of toolsToTest) {
    await runTest(`${toolName} returns graceful error`, async () => {
      const result = await executeTool(toolName, args);
      const text = result?.content?.[0]?.text;
      if (!text) throw new Error('No content returned');
      const parsed = JSON.parse(text);
      if (parsed.status !== 'waiting_for_unity') {
        throw new Error(`Expected waiting_for_unity, got: ${parsed.status}`);
      }
      if (!parsed.help) throw new Error('Missing help text');
      if (!parsed.next_steps || parsed.next_steps.length === 0) throw new Error('Missing next_steps');
      return 'graceful error with help text';
    });
  }

  // --- 4. Unknown Tool Handling ---
  console.log('\n--- 4. ERROR CASES ---');

  await runTest('Unknown tool throws error', async () => {
    try {
      await executeTool('nonexistent_tool', {});
      throw new Error('Should have thrown');
    } catch (e) {
      if (!e.message.includes('Unknown tool')) throw new Error(`Wrong error: ${e.message}`);
      return 'correctly threw Unknown tool error';
    }
  });

  // --- 5. Status Tools (work without Unity) ---
  console.log('\n--- 5. STATUS TOOLS ---');

  await runTest('unity_status:check returns hub info', async () => {
    const result = await executeTool('unity_status', { action: 'check' });
    const text = result?.content?.[0]?.text;
    if (!text) throw new Error('No content');
    // Status check should return something even without Unity
    return text.slice(0, 100);
  });

  await runTest('unity_status:transport_diagnostics returns info', async () => {
    const result = await executeTool('unity_status', { action: 'transport_diagnostics' });
    const text = result?.content?.[0]?.text;
    if (!text) throw new Error('No content');
    return text.slice(0, 100);
  });

  // --- 6. Hub Stop ---
  console.log('\n--- 6. HUB SHUTDOWN ---');

  await runTest('Hub stops cleanly', async () => {
    hub.stop();
    // Give it a moment
    await new Promise(r => setTimeout(r, 500));
    return 'stopped';
  });

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
