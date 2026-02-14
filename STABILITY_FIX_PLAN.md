# UnityVision MCP — Connection Stability Fix Plan

> **Document version:** 1.0  
> **Date:** 2026-02-14  
> **Status:** DRAFT — Awaiting APPROVE / PARTIAL_APPROVE / REVISE  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Priority #1 — Main Thread Starvation (Deep Analysis)](#2-priority-1--main-thread-starvation-deep-analysis)
   - [Option A: Hybrid Thread Execution Model](#option-a-hybrid-thread-execution-model)
   - [Option B: Platform-Native Window Message Pump](#option-b-platform-native-window-message-pump)
   - [Option C: HTTP Sidecar Server Inside Unity](#option-c-http-sidecar-server-inside-unity)
   - [Option D: Aggressive RepaintAllViews + Programmatic Focus](#option-d-aggressive-repaintallviews--programmatic-focus)
   - [Comparison Matrix](#comparison-matrix)
   - [What Other Unity MCPs Use](#what-other-unity-mcps-use)
   - [Recommendation](#recommendation)
   - [Your Previous Crash: Root Cause Analysis](#your-previous-crash-root-cause-analysis)
3. [Priority #2 — Server-Side Health Checking](#3-priority-2--server-side-health-checking)
4. [Priority #3 — Command Queue Deadlock Prevention](#4-priority-3--command-queue-deadlock-prevention)
5. [Priority #4 — Thread Safety Fixes](#5-priority-4--thread-safety-fixes)
6. [Priority #5 — Reconnect Grace Period & Session Migration](#6-priority-5--reconnect-grace-period--session-migration)
7. [Priority #6 — Dual Dispatcher Cleanup](#7-priority-6--dual-dispatcher-cleanup)
8. [Implementation Roadmap](#8-implementation-roadmap)
9. [Risk Assessment](#9-risk-assessment)
10. [Testing Strategy](#10-testing-strategy)

---

## 1. Executive Summary

The MCP bridge suffers from **8 identified issues** causing connection instability, command hangs, and "stuck forever" states. The single most impactful problem is **Unity's `EditorApplication.update` not firing when the editor is unfocused**, which starves the entire command pipeline.

This document provides:
- A deep analysis of 4 architectural options to solve the main thread starvation problem
- Detailed implementation plans for all 8 fixes
- A phased rollout roadmap with acceptance criteria
- Risk assessment and testing strategy

**Estimated total effort:** 3-5 days for all priorities.

---

## 2. Priority #1 — Main Thread Starvation (Deep Analysis)

### The Core Problem

Unity's `EditorApplication.update` callback fires at ~60 Hz when focused, but **drops to 0-4 Hz (or stops entirely) when the editor window is not focused**. This is a well-documented Unity limitation (confirmed by Unity forums, issue tracker, and the `EditorApplication.update` API docs).

The current architecture routes **100% of command processing** through this callback:

```
ReceiveLoop (background thread)
  → _pendingMessages queue
    → Update() [EditorApplication.update — BOTTLENECK]
      → ProcessPendingMessages()
        → QueueCommand()
          → ProcessNextCommand() [EditorApplication.delayCall — ALSO BOTTLENECK]
            → ExecuteCommandAsync()
              → RpcHandler.HandleRequest()
```

When Unity is unfocused, **both** `EditorApplication.update` and `EditorApplication.delayCall` stop firing reliably, creating a complete pipeline stall.

### Current Mitigation (Insufficient)

The `KeepaliveLoop` background thread (WebSocketClient.cs:223-255) sets `_needsEditorUpdate = 1`, and `Update()` calls `ForceEditorUpdate()` which calls `InternalEditorUtility.RepaintAllViews()`. 

**Why this fails:** `RepaintAllViews()` triggers a *paint pass*, not an *update tick*. In Unity's internal architecture, `EditorApplication.update` is driven by the editor's message pump, not the paint system. When the OS deprioritizes Unity's window, the message pump slows regardless of paint requests.

---

### Option A: Hybrid Thread Execution Model

**Concept:** Classify each RPC handler as either "main-thread-required" or "background-safe". Execute background-safe handlers directly on the WebSocket receive thread (or a dedicated worker thread), bypassing `EditorApplication.update` entirely. Only marshal main-thread-required operations to the update loop.

**How it works:**
1. Add a `[MainThreadRequired]` attribute to handler methods that call Unity APIs
2. Handlers WITHOUT this attribute execute immediately on a background `TaskScheduler`
3. Handlers WITH the attribute go through the existing `EditorApplication.delayCall` queue
4. The background thread pool is managed with a `SemaphoreSlim` to limit concurrency

**Which handlers are background-safe?**
- Read-only data queries that don't touch UnityEngine APIs (e.g., file reads, string processing)
- Handlers that only use `System.*` APIs

**Which handlers MUST be main thread?**
- ALL handlers that use `UnityEngine.*` or `UnityEditor.*` APIs (which is **almost all of them**)
- `GameObject.Find`, `EditorApplication.*`, `AssetDatabase.*`, `Selection.*`, etc.

**Pros:**
- Clean separation of concerns
- Background-safe handlers respond instantly regardless of focus state
- No platform-specific code
- No new dependencies
- Preserves existing architecture

**Cons:**
- **Very few handlers are actually background-safe** — nearly every Unity MCP command touches Unity APIs (`GetEditorState`, `CreateGameObject`, `GetConsoleLog`, `TakeScreenshot`, etc.). A realistic audit would find maybe 5-10% of handlers are background-safe.
- Adds complexity with the attribute system and dual execution paths
- Risk of incorrectly marking a handler as background-safe, causing Unity crashes
- **Does NOT solve the core problem** for the 90%+ of commands that need the main thread
- The "background-safe" classification could change between Unity versions

**Verdict:** Marginal improvement. Useful as a supplementary optimization, but **not a solution** to the fundamental problem.

---

### Option B: Platform-Native Window Message Pump

**Concept:** Instead of relying on `RepaintAllViews()`, use OS-level APIs to directly inject messages into Unity's window message queue, forcing the editor's internal update loop to tick.

**How it works (Windows):**
```csharp
[DllImport("user32.dll")]
static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

[DllImport("user32.dll")]
static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

// In KeepaliveLoop:
var hwnd = FindWindow("UnityContainerWndClass", null);
PostMessage(hwnd, 0x0400 /* WM_USER */, IntPtr.Zero, IntPtr.Zero);
```

**How it works (macOS):**
```csharp
// Use NSApplication sendEvent or CGEventPost to inject a synthetic event
```

**Pros:**
- Directly addresses the root cause — forces Unity's message pump to process
- `EditorApplication.update` WILL fire in response to window messages
- Low overhead — a single PostMessage is essentially free
- No architectural changes needed — the existing pipeline works as-is

**Cons:**
- **Platform-specific code** — needs separate implementations for Windows, macOS, Linux
- `FindWindow("UnityContainerWndClass")` is **undocumented and fragile** — Unity can change the window class name between versions
- May not work reliably in all scenarios (e.g., Unity modal dialogs, splash screens)
- P/Invoke adds crash risk if the window handle becomes invalid
- **macOS and Linux are significantly harder** — no simple equivalent to `PostMessage`
- Unity could theoretically ignore injected messages in future versions
- On Windows, if the user has multiple Unity editor instances, `FindWindow` may target the wrong one

**Verdict:** Effective on Windows but fragile and non-portable. High risk of breaking across Unity versions and platforms. **Not recommended as a primary solution.**

---

### Option C: HTTP Sidecar Server Inside Unity

**Concept:** Run a lightweight HTTP server on a background thread inside Unity using `System.Net.HttpListener`. This server receives commands independently of `EditorApplication.update`, queues them, and uses a combination of techniques to nudge the main thread.

**How it works:**
1. On `[InitializeOnLoad]`, start an `HttpListener` on a background thread (e.g., `http://localhost:6401/`)
2. The Node.js MCP server sends commands via HTTP POST instead of (or in addition to) WebSocket
3. The HTTP listener thread receives the request, queues it, and **holds the HTTP response open** (long-polling)
4. A dedicated "pump" thread repeatedly calls `EditorApplication.QueuePlayerLoopUpdate()` or sets the `_needsEditorUpdate` flag
5. When `EditorApplication.update` fires, it processes the queue and completes the HTTP response

**Architecture diagram:**
```
AI Client → Node.js MCP Server
              ↓ HTTP POST (holds connection open)
         HttpListener (background thread in Unity)
              ↓ queue command
         Main Thread [EditorApplication.update]
              ↓ execute handler, write result
         HttpListener completes HTTP response
              ↓
         Node.js MCP Server → AI Client
```

**Pros:**
- HTTP listener runs completely independently of the editor update loop for RECEIVING
- Long-polling pattern means the Node.js server naturally waits for the result
- No platform-specific code
- This is the architecture used by **mcp-unity (CoderGamester)** — the most popular Unity MCP project (1.3k stars)
- Clean request/response model — no need to correlate command IDs across WebSocket messages
- Built-in timeout via HTTP request timeout
- The `HttpListener` is part of .NET standard — no external dependencies

**Cons:**
- **CRITICAL: Domain reload during Play Mode will crash** — this is exactly what you experienced before (see detailed analysis below)
- Still depends on `EditorApplication.update` for actual execution (just not for receiving)
- Adds a second transport layer alongside the existing WebSocket
- `HttpListener` requires admin privileges on some Windows configurations (or URL reservation)
- Port management — now two ports instead of one
- More complex architecture overall

**Regarding the crash:** This is solvable (see section below), but it requires careful lifecycle management.

**Verdict:** Architecturally sound and proven by the market leader, but **adds significant complexity** and the domain reload issue must be handled carefully.

---

### Option D: Aggressive RepaintAllViews + Programmatic Focus

**Concept:** Enhance the existing `KeepaliveLoop` with more aggressive techniques to force Unity to tick its update loop, including programmatically bringing Unity to the foreground.

**How it works:**
1. **More aggressive RepaintAllViews** — call it every 100ms instead of 500ms when commands are pending
2. **Use `EditorApplication.QueuePlayerLoopUpdate()`** — available in Unity 2021.2+, this is specifically designed to request an extra editor update tick
3. **Programmatic focus via `EditorWindow.FocusWindowIfItsOpen`** — bring a Unity window to front before command execution
4. **Win32 `SetForegroundWindow`** as a fallback on Windows
5. **`EditorApplication.ExecuteMenuItem("Window/General/Game")`** — forces Unity to process an internal command

**Implementation:**
```csharp
private static void ForceEditorUpdate()
{
    // Method 1: Request a player loop update tick (Unity 2021.2+)
    // This is the MOST RELIABLE method for triggering EditorApplication.update
    #if UNITY_2021_2_OR_NEWER
    EditorApplication.QueuePlayerLoopUpdate();
    #endif
    
    // Method 2: Repaint all views
    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    
    // Method 3: If we have pending commands, bring Unity to front
    if (_hasPendingCommands)
    {
        // Focus the Scene View or any editor window
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
            sceneView.Focus();
    }
}
```

**Pros:**
- **Minimal code changes** — enhances existing architecture, ~20 lines changed
- No new threads, no new servers, no new ports
- No platform-specific P/Invoke (except optional Win32 fallback)
- `EditorApplication.QueuePlayerLoopUpdate()` is the **official Unity API** for this exact use case
- No domain reload issues — no background servers to manage
- Zero risk of the crash you experienced before
- Can be implemented and tested in < 1 hour

**Cons:**
- `QueuePlayerLoopUpdate()` is only available in Unity 2021.2+ (but you're likely on a newer version)
- **Focus stealing is annoying** — if Unity keeps grabbing focus while you're coding in your IDE, it disrupts workflow. This needs to be configurable or limited (e.g., only focus when a command has been waiting > 2 seconds)
- `RepaintAllViews` alone is still insufficient — it's the `QueuePlayerLoopUpdate()` that does the heavy lifting
- Still fundamentally limited by Unity's editor tick rate when unfocused — but `QueuePlayerLoopUpdate()` explicitly requests additional ticks
- The "bring to front" behavior is exactly what your colleague does manually — automating it has UX trade-offs

**Verdict:** **Best risk/reward ratio.** Minimal changes, uses official Unity APIs, no new infrastructure, and directly addresses the root cause via `QueuePlayerLoopUpdate()`.

---

### Comparison Matrix

| Criterion                        | Option A (Hybrid Thread) | Option B (Win32 PostMessage) | Option C (HTTP Sidecar) | Option D (QueuePlayerLoop) |
|----------------------------------|:------------------------:|:----------------------------:|:-----------------------:|:--------------------------:|
| Solves unfocused problem         | ~10% of commands         | Yes (Windows only)           | Partially*              | Yes                        |
| Platform portable                | Yes                      | No                           | Yes                     | Yes (2021.2+)              |
| Implementation complexity        | Medium                   | Medium                       | High                    | **Low**                    |
| Risk of Unity crashes            | Medium                   | Medium                       | **High** (domain reload)| **Low**                    |
| Lines of code changed            | ~200                     | ~80                          | ~500+                   | **~30**                    |
| New dependencies                 | None                     | P/Invoke                     | HttpListener            | None                       |
| Market-proven                    | No                       | No                           | Yes (mcp-unity)         | Partially                  |
| Solves core problem permanently  | No                       | Partially                    | Partially*              | **Yes** (with caveats)     |
| Time to implement                | 1-2 days                 | 1 day                        | 2-3 days                | **< 1 day**                |

*\* Option C still needs `EditorApplication.update` to fire for actual execution — it only decouples receiving.*

---

### What Other Unity MCPs Use

#### mcp-unity (CoderGamester) — 1.3k stars, most popular
- **Architecture:** Node.js MCP server + WebSocket server inside Unity (C#)
- **Main thread handling:** Commands received on WebSocket background thread, queued, executed on main thread via Unity's update loop
- **Key difference:** Their Node.js server is a **separate process** that bridges stdio↔WebSocket. The WebSocket server runs inside Unity. They use a configurable `REQUEST_TIMEOUT` (default 10s) and the Node.js server simply waits for the response.
- **Focus handling:** No programmatic focus — they rely on the user keeping Unity in the foreground, and their default timeout is only 10s (shorter = fail fast)
- **Domain reload:** They handle this by stopping/restarting the WebSocket server on assembly reload events

#### Unity-MCP (IvanMurzak) — 991 stars
- **Architecture:** ASP.NET Core / SignalR server (runs inside Unity or as Docker container)
- **Main thread handling:** Explicit `MainThread.Instance.Run(() => { ... })` pattern — tool authors must explicitly opt-in to main thread execution
- **Key insight:** Their documentation says: *"Note that the line `MainThread.Instance.Run(() =>` allows you to run code on the main thread. If you don't need this and running the tool in a background thread is acceptable, avoid using the main thread for efficiency purposes."*
- **This is essentially Option A** (hybrid thread model), but with the crucial difference that they run a full ASP.NET server which has its own request/response lifecycle independent of Unity's update loop

#### CoplayDev/unity-mcp
- **Architecture:** Python MCP server + WebSocket bridge
- **Similar to CoderGamester** — external process bridges to Unity WebSocket

#### Key Insight from Competitors
All three competitors use the pattern: **external MCP server process → WebSocket → Unity**. The critical difference is that **none of them have solved the unfocused-editor problem elegantly**. They either:
1. Accept shorter timeouts and let the AI retry (CoderGamester: 10s timeout)
2. Require the user to keep Unity focused
3. Use `MainThread.Run()` to explicitly minimize main-thread usage (IvanMurzak)

**No competitor uses `EditorApplication.QueuePlayerLoopUpdate()`** — this is likely because it's a relatively new API (Unity 2021.2+) and not widely known.

---

### Recommendation

**Primary: Option D (QueuePlayerLoopUpdate + configurable auto-focus)**

Reasons:
1. **Lowest risk** — no new infrastructure, no domain reload issues, no platform-specific code
2. **Uses the official Unity API** designed for exactly this use case
3. **Fastest to implement** — can be tested within hours
4. **Directly addresses** what your colleague discovered (focus = commands work)
5. **No competitor has tried this** — potential differentiator

**Secondary: Option A (Hybrid Thread) as a supplementary optimization**

After Option D is in place and working, identify any read-only handlers that genuinely don't need Unity APIs and execute them on the background thread. This is a nice-to-have optimization, not a critical fix.

**NOT recommended: Option C (HTTP Sidecar)**

While architecturally proven by mcp-unity, it adds massive complexity and the domain reload crash risk. The benefits (decoupled receiving) don't justify the cost when `QueuePlayerLoopUpdate()` can force the update loop to tick.

---

### Your Previous Crash: Root Cause Analysis

You mentioned that a background execution approach crashed Unity when hitting Play. Here's exactly why:

#### The Problem: Domain Reload Kills Background Threads Mid-Operation

When you press Play in Unity:
1. Unity triggers **domain reload** — it unloads all managed assemblies and reloads them
2. All static fields are reset to their initial values
3. All managed threads are **aborted** (via `ThreadAbortException`)
4. All `async` operations are cancelled
5. Any `Socket`, `HttpListener`, `TcpListener`, or `WebSocket` objects become invalid

**The crash happens when:**
- A background thread is in the middle of a **blocking socket operation** (e.g., `Socket.Poll(-1, ...)`, `HttpListener.GetContext()`, `WebSocket.ReceiveAsync()`)
- Unity tries to abort the thread during domain reload
- The native socket code **cannot be interrupted** — it's blocked in OS-level `select()` or `poll()`
- Unity's domain reload hangs waiting for the thread to terminate
- **Result: Editor frozen on "Reloading Domain" indefinitely**

This is a **confirmed Unity bug** (UUM-104606), marked as **"Won't Fix"** by Unity:
> *"The Socket.Poll method does call into the operating system's native select function. The native select function blocks in operating system code with no way to exit the wait. This means we are unable to exit the thread that calls Socket.Poll during domain reload."*

#### How to Avoid This (If Option C Were Chosen)

1. **Never use blocking socket calls** — always use async with `CancellationToken`
2. **Set short timeouts** on all socket operations (e.g., 1 second max)
3. **Stop the listener BEFORE domain reload** via `AssemblyReloadEvents.beforeAssemblyReload`
4. **Use `Thread.IsBackground = true`** so threads don't prevent domain reload
5. **Set socket `ReceiveTimeout` and `SendTimeout`** to small values (1000ms)
6. **Cancel the CancellationTokenSource** in `beforeAssemblyReload`, then `Dispose()` the listener
7. **Never call `Thread.Join()`** during domain reload — just abandon the thread

The current code already does most of this correctly (see `WebSocketClient.cs:289-296`), but the key issue is that `ClientWebSocket.ReceiveAsync` internally calls into native socket code that **cannot be cancelled** on some platforms.

#### Why Option D Avoids This Entirely
Option D doesn't create any new background servers, listeners, or blocking socket operations. It only enhances the existing `ForceEditorUpdate()` method. There is **zero domain reload risk**.

---

## 3. Priority #2 — Server-Side Health Checking

### Problem
- Unity sends `type: "ping"` messages but the server handles them as "Unknown message type"
- The server declares `KEEP_ALIVE_INTERVAL` and `SERVER_TIMEOUT` constants but **never uses them**
- Dead connections (TCP half-open) are never detected — stale sessions persist indefinitely

### Files to Modify
- `unity-mcp-server/src/websocketHub.ts`

### Implementation Plan

#### Step 2.1: Handle Incoming Pings
In `handleMessage()` switch statement, add a `"ping"` case that responds with `"pong"`:

```typescript
case 'ping':
    this.handlePing(ws, data);
    break;
```

```typescript
private handlePing(ws: WebSocket, data: any): void {
    const sessionId = data.session_id;
    const session = this.findSessionByWs(ws);
    if (session) {
        session.lastPing = Date.now();
        ws.send(JSON.stringify({ type: 'pong' }));
    }
}
```

#### Step 2.2: Start Server-Side Heartbeat
In `start()`, begin an interval that checks session health:

```typescript
this.keepAliveTimer = setInterval(() => {
    const now = Date.now();
    for (const [id, session] of this.sessions) {
        const timeSinceLastPing = now - session.lastPing;
        if (timeSinceLastPing > SERVER_TIMEOUT) {
            fileLog('WARN', 'WebSocketHub', `Session ${id} timed out (no ping for ${timeSinceLastPing}ms)`);
            this.handleDisconnect(session.ws, id);
            session.ws.close();
        }
    }
}, KEEP_ALIVE_INTERVAL);
```

#### Step 2.3: Track Session per Command
Add `sessionId` to the `PendingCommand` type so commands can be rejected on disconnect.

### Acceptance Criteria
- [ ] Server responds to `ping` with `pong`
- [ ] Sessions with no ping for >30s are automatically closed
- [ ] Pending commands for disconnected sessions are rejected immediately
- [ ] Log file shows heartbeat activity

---

## 4. Priority #3 — Command Queue Deadlock Prevention

### Problem
- Commands can sit in the Unity-side queue forever if `EditorApplication.delayCall` doesn't fire
- Server-side pending commands hang for 30s after disconnect ("let them timeout naturally")
- No circuit breaker — failures cascade into multi-minute hangs

### Files to Modify
- `WebSocketClient.cs`
- `websocketHub.ts`

### Implementation Plan

#### Step 3.1: Stale Command Reaper (Unity Side)
Add a timestamp check in `Update()` that catches commands stuck longer than their timeout:

```csharp
// In Update(), after ProcessPendingMessages():
ReapStaleCommands();
```

```csharp
private static void ReapStaleCommands()
{
    lock (_queueLock)
    {
        while (_commandQueue.Count > 0)
        {
            var front = _commandQueue.Peek();
            var age = (DateTime.Now - front.QueuedAt).TotalSeconds;
            if (age > 30) // Same as server timeout
            {
                _commandQueue.Dequeue();
                // Send timeout error
                var timeoutResult = new JObject
                {
                    ["type"] = "command_result",
                    ["id"] = front.Id,
                    ["error"] = new JObject
                    {
                        ["code"] = "QUEUE_TIMEOUT",
                        ["message"] = $"Command '{front.Name}' expired after {age:F0}s in queue"
                    }
                };
                _ = SendMessageAsync(timeoutResult);
            }
            else break; // Queue is FIFO, so if front isn't stale, nothing behind it is
        }
    }
}
```

#### Step 3.2: Immediate Reject on Disconnect (Server Side)
In `handleDisconnect()`, reject all pending commands for that session instead of the current comment "let them timeout naturally":

```typescript
// Track which session owns each pending command
private pendingCommandSessions = new Map<string, string>(); // commandId → sessionId

// In handleDisconnect:
for (const [cmdId, sessionId] of this.pendingCommandSessions) {
    if (sessionId === disconnectedSessionId) {
        const pending = this.pendingCommands.get(cmdId);
        if (pending) {
            pending.reject(new Error('Unity session disconnected'));
            this.pendingCommands.delete(cmdId);
        }
        this.pendingCommandSessions.delete(cmdId);
    }
}
```

#### Step 3.3: Circuit Breaker
Track consecutive failures. After 3 consecutive timeouts, pause command sending for 10s and report to the AI client:

```typescript
private consecutiveFailures = 0;
private circuitBreakerUntil = 0;

// In sendCommand:
if (Date.now() < this.circuitBreakerUntil) {
    throw new UnityBridgeError('CIRCUIT_OPEN', 
        'Too many consecutive failures. Unity may be unresponsive. Waiting before retry...');
}
```

### Acceptance Criteria
- [ ] Commands stuck >30s in Unity queue are auto-reaped with error response
- [ ] Server-side pending commands are rejected immediately on disconnect
- [ ] After 3 consecutive timeouts, new commands get a fast "circuit open" error
- [ ] Circuit breaker auto-resets after 10s

---

## 5. Priority #4 — Thread Safety Fixes

### Problem
- `_isConnected` and `_isConnecting` are plain `bool` fields accessed from multiple threads
- `async void` methods can swallow exceptions

### Files to Modify
- `WebSocketClient.cs`

### Implementation Plan

#### Step 4.1: Volatile/Interlocked for Connection State
Change connection state flags to use proper synchronization:

```csharp
// Replace:
private static bool _isConnected = false;
private static bool _isConnecting = false;

// With:
private static volatile bool _isConnected = false;
private static volatile bool _isConnecting = false;
```

Or better, use a state enum with `Interlocked`:

```csharp
private static int _connectionState = (int)ConnectionState.Disconnected;

private enum ConnectionState { Disconnected = 0, Connecting = 1, Connected = 2 }

private static bool IsConnected => 
    (ConnectionState)Interlocked.CompareExchange(ref _connectionState, 0, 0) == ConnectionState.Connected;
```

#### Step 4.2: Audit async void
Wrap `ConnectAsync()` and `ExecuteCommandAsync()` fire-and-forget calls with proper error handling:

```csharp
// Change from:
private static async void ConnectAsync() { ... }

// To helper pattern:
private static void ConnectAsync()
{
    _ = ConnectAsyncInternal().ContinueWith(t => 
    {
        if (t.IsFaulted)
            FileLogger.Log("ERROR", "WebSocketClient", $"Connect failed: {t.Exception?.InnerException?.Message}");
    }, TaskScheduler.FromCurrentSynchronizationContext());
}

private static async Task ConnectAsyncInternal() { ... }
```

### Acceptance Criteria
- [ ] No race conditions possible between connection state transitions
- [ ] All `async void` methods have proper exception logging
- [ ] No unobserved `TaskException` warnings in Unity console

---

## 6. Priority #5 — Reconnect Grace Period & Session Migration

### Problem
- `RECONNECT_GRACE_PERIOD = 10000` (10s) — too short for large project assembly reloads
- When a session is replaced, pending commands for the old session are lost

### Files to Modify
- `websocketHub.ts`

### Implementation Plan

#### Step 5.1: Increase Grace Period
```typescript
// Change from:
private static readonly RECONNECT_GRACE_PERIOD = 10_000;

// To:
private static readonly RECONNECT_GRACE_PERIOD = 45_000; // 45 seconds
```

#### Step 5.2: Session Migration
When a new session registers with the same `project_hash`, resolve any pending `resolveSession` waiters immediately:

```typescript
// In handleRegister, after replacing the session:
// Resolve any pending session waiters immediately
for (const waiter of this.sessionWaiters) {
    waiter.resolve(newSession);
}
this.sessionWaiters = [];
```

### Acceptance Criteria
- [ ] Assembly reloads up to 45s don't cause "No Unity instance" errors
- [ ] Pending `resolveSession` calls resolve immediately when new session registers

---

## 7. Priority #6 — Dual Dispatcher Cleanup

### Problem
`CommandDispatcher.cs` has its own independent command dispatch pipeline that overlaps with `WebSocketClient.cs`'s built-in queue.

### Files to Modify
- `CommandDispatcher.cs`
- `WebSocketClient.cs`

### Implementation Plan

#### Step 6.1: Audit Usage
Determine if `CommandDispatcher` is actually used by any code path. If it's dead code, mark it as deprecated. If it's used by the Named Pipe transport, consolidate so there's a single command pipeline.

#### Step 6.2: Consolidate (if dead code)
Add `[Obsolete]` attribute and route any remaining callers through `WebSocketClient`'s queue.

### Acceptance Criteria
- [ ] Single command dispatch pipeline
- [ ] No duplicate update hooks for command processing

---

## 8. Implementation Roadmap

### Phase 1: Quick Wins (Day 1) — Immediate Stability Improvement

| Step | Priority | Description | Risk |
|------|----------|-------------|------|
| 1.1  | P1 (Option D) | Add `QueuePlayerLoopUpdate()` to `ForceEditorUpdate()` | Low |
| 1.2  | P1 (Option D) | Add configurable auto-focus with 2s delay | Low |
| 1.3  | P2 | Handle `ping` messages in WebSocket hub | Low |
| 1.4  | P4 | Make `_isConnected`/`_isConnecting` volatile | Low |

**Approval checkpoint:** Test connection with Unity unfocused for 5+ minutes with multiple commands.

### Phase 2: Server Hardening (Day 2)

| Step | Priority | Description | Risk |
|------|----------|-------------|------|
| 2.1  | P2 | Implement server-side heartbeat interval | Low |
| 2.2  | P2 | Track session per pending command | Medium |
| 2.3  | P3 | Add stale command reaper on Unity side | Low |
| 2.4  | P3 | Immediate reject on disconnect (server) | Medium |

**Approval checkpoint:** Simulate disconnect → verify no 30s hangs.

### Phase 3: Robustness (Day 3)

| Step | Priority | Description | Risk |
|------|----------|-------------|------|
| 3.1  | P3 | Circuit breaker implementation | Medium |
| 3.2  | P4 | `async void` audit and fix | Low |
| 3.3  | P5 | Increase grace period to 45s | Low |
| 3.4  | P5 | Session migration on re-register | Medium |

**Approval checkpoint:** Full integration test with assembly reload + play mode transitions.

### Phase 4: Cleanup (Day 4-5)

| Step | Priority | Description | Risk |
|------|----------|-------------|------|
| 4.1  | P6 | Audit `CommandDispatcher` usage | Low |
| 4.2  | P6 | Consolidate or deprecate | Medium |
| 4.3  | — | End-to-end stress test | — |
| 4.4  | — | Documentation update | — |

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `QueuePlayerLoopUpdate()` not available on target Unity version | Low | High | Feature-gate with `#if UNITY_2021_2_OR_NEWER`, fall back to `RepaintAllViews` |
| Auto-focus disrupts developer workflow | Medium | Medium | Make configurable with EditorPrefs toggle, default=enabled with 2s delay |
| Breaking change in WebSocket protocol (ping handling) | Low | Low | Backward compatible — old clients just won't get pong responses |
| Circuit breaker triggers false positives | Low | Medium | Conservative threshold (3 failures), auto-reset after 10s |
| Assembly reload race condition with new session migration | Medium | Medium | Use locks and atomic state transitions |

---

## 10. Testing Strategy

### Manual Tests
1. **Unfocused command execution:** Switch to IDE, send 5 commands in a row, verify all complete within 5s
2. **Disconnect recovery:** Kill the Node.js server mid-command, restart it, verify reconnection + no stale commands
3. **Play mode transition:** Enter play mode → exit play mode → send command → verify success
4. **Assembly reload:** Modify a C# file → save → wait for recompile → send command → verify success
5. **Long idle recovery:** Leave Unity unfocused for 10 minutes → send command → verify success
6. **Stress test:** Send 20 rapid-fire commands → verify all complete without deadlock

### Automated Verification
- Add a `/health` diagnostic command that returns queue depth, connection state, last activity timestamp
- Server-side logging for all heartbeat events and session transitions
- Unity-side `FileLogger` entries for all force-update triggers

---

## Decision Required

**APPROVE** — Proceed with implementation starting from Phase 1  
**PARTIAL_APPROVE: Phase N** — Proceed with specific phases only  
**REVISE: \<notes\>** — Modify the plan based on feedback
