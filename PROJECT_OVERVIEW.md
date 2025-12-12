# PROJECT_OVERVIEW.md — UnityVision MCP Server

**Status as of December 8, 2025**

---

## 1. Executive Summary

### What is UnityVision?

UnityVision is a **Model Context Protocol (MCP) server** that provides AI assistants (Claude, Windsurf/Cascade, Cursor, etc.) with deep, programmatic access to the Unity Editor. It enables AI to see, understand, and control Unity projects through a standardized protocol.

### Vision

To be the most powerful and feature-complete AI bridge for Unity development, enabling seamless collaboration between developers and AI assistants for game development tasks.

### Target Users

- **Unity Developers** using AI-assisted coding tools (Windsurf, Cursor, Claude Desktop)
- **VR/XR Developers** who need AI help with spatial development
- **Game Studios** looking to accelerate development with AI automation
- **Solo Developers** who want AI pair programming for Unity

### Current Status

**Production Ready (v1.1.0)** — All core features implemented and tested:
- 26+ consolidated MCP tools
- 5 MCP Resources
- WebSocket transport with auto-reconnect
- Multi-project support
- 102 unit tests passing

---

## 2. Current Status

### What's Built ✅

| Category | Status | Details |
|----------|--------|---------|
| MCP Server | ✅ Complete | Node.js/TypeScript, 26 tools, 5 resources |
| Unity Bridge | ✅ Complete | C# package, WebSocket client, 15+ handlers |
| Transport | ✅ Complete | WebSocket (primary), HTTP (legacy fallback) |
| Multi-Project | ✅ Complete | Auto-discovery, project switching |
| Script Management | ✅ Complete | CRUD, atomic writes, path protection |
| Test Runner | ✅ Complete | Unity Test Framework integration |
| Custom Tools | ✅ Complete | Reflection-based auto-discovery |
| Security | ✅ Complete | Path traversal protection, optional auth |

### What's Partially Built 🔶

| Feature | Status | Notes |
|---------|--------|-------|
| npm Publishing | 🔶 Ready | Package.json configured, not yet published |
| GitHub Actions | 🔶 Planned | CI/CD pipeline not yet set up |

### What's Missing ❌

| Feature | Priority | Notes |
|---------|----------|-------|
| Integration Tests | Medium | Unit tests exist, integration tests planned |
| Documentation Site | Low | README is comprehensive |
| Video Tutorials | Low | Nice to have |

### Technical Risks

1. **Unity API Changes** — Unity Editor APIs may change between versions
2. **WebSocket Stability** — Long-running connections may need reconnection handling (implemented)
3. **Reflection Usage** — Console log access uses internal Unity APIs that may change

---

## 3. Features Overview

### Implemented Features (v1.1.0)

#### Core Tools (26 Consolidated)
- `unity_editor` — Editor state, play mode, recompile, refresh
- `unity_console` — Console logs with file/line info
- `unity_scene` — Scene CRUD operations
- `unity_gameobject` — GameObject CRUD with auto-create hierarchy
- `unity_component` — Component management
- `unity_asset` — Asset database operations
- `unity_script` — Script CRUD with atomic writes
- `unity_package` — Unity Package Manager
- `unity_screenshot` — Game/Scene view capture
- `unity_xr` — VR/XR rig control
- `unity_code` — C# code execution
- `unity_test` — Test runner
- `unity_build` — Build player
- `unity_batch` — Batch operations
- And more...

#### MCP Resources (5)
- `unity://hierarchy` — Scene hierarchy tree
- `unity://selection` — Currently selected objects
- `unity://logs` — Console log messages
- `unity://project-info` — Project metadata
- `unity://editor-state` — Editor play mode, compilation status

#### Phase 50 Features
- Custom JSON converters for Unity types
- Auto-create GameObject hierarchy
- Reflection-based console access with file/line
- Async recompilation with message tracking
- Auto-create tags
- Atomic file writes
- Path traversal protection
- Apply text edits action

### MVP Scope (v1.0)
- All 26 tools functional
- WebSocket transport working
- Multi-project support
- Basic documentation

### V2 Features (Planned)
- Live Component Inspector
- Material & Shader Tools
- Advanced Scene Queries
- Performance Profiler Integration
- Frame Debugger Integration
- Animation Tools
- Audio Tools

---

## 4. User Personas & Use Cases

### Persona 1: Solo Unity Developer

**Goals:**
- Speed up repetitive tasks
- Get AI help debugging issues
- Automate scene setup

**Use Cases:**
- "Create a player controller with WASD movement"
- "Find all objects with missing references"
- "Take a screenshot and tell me what's wrong with my UI"

### Persona 2: VR/XR Developer

**Goals:**
- Test VR interactions without headset
- Position XR rigs programmatically
- Debug spatial issues

**Use Cases:**
- "Position the XR rig at the spawn point"
- "Teleport to the teleport anchor named 'Kitchen'"
- "Show me the VR camera's current view"

### Persona 3: Team Lead / Tech Director

**Goals:**
- Standardize project setup
- Automate code reviews
- Enforce coding standards

**Use Cases:**
- "Run all unit tests and show me failures"
- "Find all scripts without namespace"
- "Create folder structure for new feature"

---

## 5. User Flows

### Flow 1: Debugging a NullReferenceException

```
User: "My game crashes with a NullReferenceException"
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_console({ action: 'get_logs', logType: 'error' })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI reads stack trace, identifies file/line
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_script({ action: 'read', path: '...' })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI identifies bug, proposes fix
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_script({ action: 'update', ... })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_editor({ action: 'recompile' })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI verifies fix with get_compilation_status
└─────────────────────────────────────────┘
```

### Flow 2: Creating a UI Layout

```
User: "Create a main menu with Play and Quit buttons"
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_menu({ action: 'execute', menuPath: 'GameObject/UI/Canvas' })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_gameobject({ action: 'create', ... }) for buttons
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_component({ action: 'set_properties', ... })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI calls: unity_screenshot({ action: 'game_view' })
└─────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────┐
│ AI shows result to user for verification
└─────────────────────────────────────────┘
```

---

## 6. Architecture Overview

### System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    AI Assistant (Claude, Windsurf, Cursor)       │
│                          MCP Client                              │
└─────────────────────────────────────────────────────────────────┘
                                │
                                │ stdio (MCP Protocol)
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    UnityVision MCP Server                        │
│                     (Node.js + TypeScript)                       │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │
│  │ Tool Registry │  │ Resources    │  │ WebSocket Hub│           │
│  │ (26 tools)    │  │ (5 resources)│  │ (port 7890)  │           │
│  └──────────────┘  └──────────────┘  └──────────────┘           │
└─────────────────────────────────────────────────────────────────┘
                                ▲
                                │ WebSocket ws://localhost:7890
                                │
┌─────────────────────────────────────────────────────────────────┐
│                    Unity Editor                                  │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │           UnityVision Bridge (C# Package)                 │   │
│  │                                                           │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐       │   │
│  │  │ WebSocket   │  │ RPC Handler │  │ Tool        │       │   │
│  │  │ Client      │  │ (dispatch)  │  │ Registry    │       │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘       │   │
│  │                                                           │   │
│  │  ┌─────────────────────────────────────────────────────┐ │   │
│  │  │                    Handlers (15+)                    │ │   │
│  │  │  Editor | Scene | GameObject | Component | Asset    │ │   │
│  │  │  Script | Console | Build | Test | XR | ...         │ │   │
│  │  └─────────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

1. **WebSocket Transport** — Unity is the WebSocket CLIENT, MCP server is the SERVER. This allows Unity to reconnect after domain reloads.

2. **Consolidated Tools** — 26 tools with `action` parameter instead of 100+ individual tools. Keeps tool count manageable for AI.

3. **Main Thread Dispatch** — All Unity API calls queued and executed on main thread via `EditorApplication.update`.

4. **Atomic File Writes** — Scripts written to `.tmp` then moved to prevent corruption.

5. **Path Traversal Protection** — All file paths validated to stay within `Assets/` or `Packages/`.

---

## 7. Data Models / Schemas

### RPC Request/Response

```typescript
// Request from MCP server to Unity
interface RpcRequest {
  jsonrpc: "2.0";
  id: string;
  method: string;
  params: Record<string, any>;
}

// Response from Unity to MCP server
interface RpcResponse {
  jsonrpc: "2.0";
  id: string;
  result?: any;
  error?: {
    code: string;
    message: string;
  };
}
```

### Tool Input Schemas

```typescript
// Example: unity_gameobject tool
interface GameObjectInput {
  action: "create" | "modify" | "delete";
  name?: string;
  path?: string;
  parent?: string;
  primitiveType?: "Cube" | "Sphere" | "Cylinder" | ...;
  components?: string[];
  position?: { x: number; y: number; z: number };
  rotation?: { x: number; y: number; z: number };
  scale?: { x: number; y: number; z: number };
  dryRun?: boolean;
  confirm?: boolean;  // Required for delete
}

// Example: unity_script tool
interface ScriptInput {
  action: "create" | "read" | "update" | "delete" | "validate" | "get_sha" | "apply_text_edits";
  path: string;
  contents?: string;
  contentsEncoded?: boolean;  // Base64
  template?: "MonoBehaviour" | "ScriptableObject" | "Editor" | "EditorWindow";
  className?: string;
  namespace?: string;
  precondition_sha256?: string;  // For conflict detection
  edits?: Array<{ startLine: number; endLine: number; newText: string }>;
}
```

### Unity Handler Response Types

```csharp
// Console log entry with file/line info
public class LogEntry
{
    public long timestamp;
    public string type;
    public string message;
    public string stackTrace;
    public string file;      // NEW: Source file path
    public int line;         // NEW: Line number
    public int instanceId;   // NEW: Unity object instance ID
}

// Compilation status response
public class CompilationStatusResponse
{
    public bool isCompiling;
    public bool hasErrors;
    public bool hasWarnings;
    public int errorCount;
    public int warningCount;
    public List<CompilerMessage> messages;
}
```

---

## 8. API Endpoints

### MCP Tools (via stdio)

All tools are called via MCP protocol over stdio. The MCP server translates these to WebSocket RPC calls to Unity.

| Tool | Actions | Description |
|------|---------|-------------|
| `unity_editor` | `get_state`, `set_play_mode`, `get_context`, `recompile`, `refresh` | Editor control |
| `unity_console` | `get_logs`, `clear` | Console management |
| `unity_scene` | `list`, `hierarchy`, `create`, `load`, `save`, `delete` | Scene CRUD |
| `unity_gameobject` | `create`, `modify`, `delete` | GameObject CRUD |
| `unity_component` | `search`, `add`, `set_properties`, `get_properties` | Components |
| `unity_script` | `create`, `read`, `update`, `delete`, `validate`, `get_sha`, `apply_text_edits` | Scripts |
| `unity_screenshot` | `game_view`, `scene_view` | Screenshots |

### WebSocket Protocol (Unity ↔ MCP Server)

```
Unity connects to: ws://localhost:7890

Messages:
- Unity → Server: { type: "register_tools", tools: [...] }
- Server → Unity: { jsonrpc: "2.0", method: "...", params: {...} }
- Unity → Server: { jsonrpc: "2.0", result: {...} }
```

---

## 9. State Management & Core Logic

### MCP Server State

- **WebSocket Hub** — Tracks connected Unity clients
- **Tool Registry** — Static list of 26 tools + dynamic custom tools from Unity
- **Project Registry** — Tracks multiple Unity instances via `~/.unityvision/projects.json`

### Unity Bridge State

- **WebSocket Client** — Connection state, reconnect backoff
- **Request Queue** — Pending RPC requests awaiting main thread execution
- **Log Buffer** — Circular buffer of recent console logs (max 1000)
- **Compilation State** — Tracking async recompilation progress

### Core Algorithms

1. **Main Thread Dispatch**
   ```csharp
   // Queue request from WebSocket thread
   _requestQueue.Enqueue(request);
   
   // Process on main thread via EditorApplication.update
   EditorApplication.update += ProcessQueue;
   ```

2. **Atomic File Write**
   ```csharp
   // Write to temp file
   File.WriteAllText(path + ".tmp", contents, Encoding.UTF8);
   // Atomic move
   File.Move(path + ".tmp", path);
   ```

3. **Path Traversal Protection**
   ```csharp
   // Normalize and validate
   var fullPath = Path.GetFullPath(path);
   var assetsPath = Path.GetFullPath("Assets");
   if (!fullPath.StartsWith(assetsPath)) return error;
   ```

---

## 10. Deployment & Environments

### Local Development Setup

```bash
# 1. Clone repository
git clone https://github.com/nicweberdev/UnityVision.git
cd UnityVision

# 2. Build MCP server
cd unity-mcp-server
npm install
npm run build

# 3. Add Unity package
# In Unity: Window > Package Manager > + > Add from disk
# Select: Packages/com.unityvision.bridge/package.json

# 4. Configure AI client
# Add to mcp_config.json:
{
  "mcpServers": {
    "unity-vision": {
      "command": "node",
      "args": ["/path/to/UnityVision/unity-mcp-server/dist/server.js"]
    }
  }
}
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_VISION_WS_PORT` | `7890` | WebSocket server port |
| `UNITY_VISION_PORT` | Auto | Override Unity HTTP port |
| `UNITY_VISION_REQUIRE_AUTH` | `false` | Enable session token auth |

### Build Commands

```bash
# MCP Server
npm run build          # Compile TypeScript
npm run dev            # Watch mode
npm run test           # Run 102 unit tests
npm run test:coverage  # Coverage report

# Unity Package
# No build step - C# compiles in Unity Editor
```

---

## 11. Testing & Quality

### Existing Tests

- **102 Jest Unit Tests** — All passing
- **Coverage**: Tool handlers, WebSocket hub, project registry

### Test Commands

```bash
cd unity-mcp-server
npm test                    # Run all tests
npm run test:watch          # Watch mode
npm run test:coverage       # Generate coverage report
```

### Manual Test Cases

1. **Connection Test**
   - Start Unity with package installed
   - Verify "CONNECTED" in Bridge Status window
   - Call `unity_editor({ action: 'get_state' })`

2. **Script CRUD Test**
   - Create script: `unity_script({ action: 'create', path: 'Assets/Test.cs' })`
   - Read script: `unity_script({ action: 'read', path: 'Assets/Test.cs' })`
   - Update script: `unity_script({ action: 'update', path: 'Assets/Test.cs', contents: '...' })`
   - Delete script: `unity_script({ action: 'delete', path: 'Assets/Test.cs' })`

3. **Screenshot Test**
   - Call `unity_screenshot({ action: 'game_view' })`
   - Verify base64 PNG returned

### Testing Gaps

- Integration tests with real Unity instance
- Performance benchmarks
- Stress testing (many concurrent requests)

---

## 12. Design & UX Notes

### Unity Editor UI

- **Bridge Status Window** — `Window > UnityVision > Bridge Status`
  - Shows connection status (CONNECTED/DISCONNECTED)
  - Recent request activity
  - Configuration helper for AI clients

### Key UX Principles

1. **Zero Configuration** — Works out of the box with auto-port assignment
2. **Full Undo** — Every AI action can be undone with Ctrl+Z
3. **Dry-Run Mode** — Preview changes before applying
4. **Confirmation Required** — Destructive actions need explicit confirmation

---

## 13. Roadmap

### Immediate Next Steps

1. ⏳ Publish to npm registry
2. ⏳ Set up GitHub Actions CI/CD
3. ⏳ Create video tutorials

### V2 Features (Planned)

- Live Component Inspector
- Material & Shader Tools
- Advanced Scene Queries
- Performance Profiler Integration
- Frame Debugger Integration
- Animation Tools
- Audio Tools

### Long-Term Vision

- Unity Cloud integration
- Collaborative AI sessions
- Custom AI model fine-tuning for Unity

---

## 14. Open Questions & Gaps

### Assumptions Made

1. Unity 2021.3+ is required (uses modern APIs)
2. Node.js 18+ is required (ES modules)
3. AI client supports MCP protocol

### Missing Information

1. Exact Unity API version compatibility matrix
2. Performance benchmarks for large scenes
3. Memory usage under heavy load

---

## 15. Developer Handover Notes

### How to Run Locally

```bash
# Terminal 1: Start Unity with package
# Open Unity project with com.unityvision.bridge package

# Terminal 2: AI client will start MCP server automatically
# Or manually: node unity-mcp-server/dist/server.js
```

### How to Extend

1. **Add New Tool**
   - Create handler in `Packages/.../Editor/Handlers/`
   - Register in `RpcHandler.cs`
   - Add tool definition in `unity-mcp-server/src/tools/`

2. **Add Custom Unity Tool**
   - Create class inheriting from `McpToolBase`
   - Implement `Execute()` or `ExecuteAsync()`
   - Tool auto-discovered via reflection

### Known Pitfalls

1. **Domain Reload** — WebSocket disconnects during script recompilation. Auto-reconnect handles this.

2. **Main Thread** — All Unity API calls must be on main thread. Use `MainThreadDispatcher` if needed.

3. **Path Separators** — Use forward slashes `/` in paths, even on Windows.

### Edge Cases

1. **Multiple Unity Instances** — Each gets unique port (7890-7899)
2. **Play Mode** — Some operations disabled during play mode
3. **Prefab Mode** — Scene hierarchy shows prefab contents

---

*Generated by UnityVision Project Overview Workflow*
*Status as of December 8, 2025*
