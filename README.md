<div align="center">

# 🎮 UnityVision MCP

**The Most Powerful AI Bridge for Unity Editor**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity](https://img.shields.io/badge/Unity-2021.3%2B-black.svg)](https://unity.com/)
[![Node.js](https://img.shields.io/badge/Node.js-18%2B-green.svg)](https://nodejs.org/)
[![MCP](https://img.shields.io/badge/MCP-Compatible-blue.svg)](https://modelcontextprotocol.io/)
[![Tests](https://img.shields.io/badge/Tests-102%20passing-brightgreen.svg)]()

*Let AI assistants see, understand, and control your Unity projects*

[Features](#-features) • [Installation](#-installation) • [Quick Start](#-quick-start) • [Tools](#-available-tools-26) • [Examples](#-example-workflows)

</div>

---

## 🚀 What is UnityVision?

UnityVision is a **Model Context Protocol (MCP)** server that gives AI assistants like **Claude**, **Windsurf/Cascade**, **Cursor**, and others deep, programmatic access to the Unity Editor. 

Instead of copy-pasting error messages or describing your scene hierarchy, the AI can:
- 📸 **See** your game through screenshots
- 🔍 **Inspect** your scene hierarchy and components
- ✏️ **Modify** GameObjects, components, and assets
- 🐛 **Debug** by reading console logs and fixing code
- 🧪 **Test** by running your test suite and iterating
- 🏗️ **Build** your project for any platform

---

## ✨ Features

### Unique Capabilities (No Other Unity MCP Has These)

| Feature | Description |
|---------|-------------|
| 📸 **Screenshot Capture** | AI can capture Game View and Scene View screenshots to visually understand your project |
| 🥽 **XR/VR Support** | Position XR rigs, teleport to anchors - perfect for VR development |
| 🎨 **UI Layout Inspection** | Dump entire UI hierarchies with RectTransform data |
| ⚡ **Batch Operations** | Execute multiple operations in one call with atomic rollback |
| 🔄 **Dry-Run Mode** | Preview what changes will be made before applying them |
| ↩️ **Full Undo Support** | Every mutation uses Unity's Undo system - Ctrl+Z always works |
| 🧠 **Smart Context** | Get selection, errors, and play state in a single optimized call |
| 🔐 **Session Authentication** | Optional token-based auth for security |
| 🔀 **Multi-Project Support** | Work with multiple Unity instances simultaneously - auto-discovery and switching |
| 🔌 **Zero-Config Ports** | Auto-assigns ports (7890-7899) to avoid conflicts |
| 🔧 **Custom Tool Registration** | Unity projects can define custom MCP tools auto-discovered via reflection |
| 🧪 **Test Runner Integration** | Run Unity Test Framework tests (EditMode/PlayMode) via AI |
| 📝 **Script Management** | Create, read, update, delete C# scripts with atomic writes and path protection |
| 🔒 **Path Traversal Protection** | Security-hardened file operations prevent writing outside project |
| 📊 **Compilation Tracking** | Async recompilation with detailed error/warning messages and file locations |

---

## 📦 Installation

### Prerequisites

- **Node.js** 18 or higher
- **Unity** 2021.3 LTS or higher (see compatibility table below)
- An **MCP-compatible AI client** (Windsurf, Claude Desktop, Cursor, etc.)

### Unity Version Compatibility

| Unity Version | Status | Notes |
|---------------|--------|-------|
| **Unity 6 (6000.x)** | ✅ Tested | Primary development version |
| Unity 2023.x | ⚠️ Should work | Not extensively tested |
| Unity 2022.3 LTS | ⚠️ Should work | Not extensively tested |
| Unity 2021.3 LTS | ⚠️ Should work | Minimum supported version |
| Unity 2020.x and earlier | ❌ Not supported | Missing required APIs |

> **Note:** UnityVision uses some internal Unity Editor APIs via reflection (e.g., for console log file/line info). These may change between Unity versions. If you encounter issues, please [report them](https://github.com/nicweberdev/UnityVision/issues).

### Step 1: Clone the Repository

```bash
git clone https://github.com/nicweberdev/UnityVision.git
cd UnityVision
```

### Step 2: Install the Unity Package

**Option A: Add via Package Manager UI (Recommended)**
1. Open Unity
2. Go to `Window > Package Manager`
3. Click `+` → `Add package from git URL...`
4. Paste: `https://github.com/nicweberdev/UnityVision.git?path=/Packages/com.unityvision.bridge`

**Option B: Add via manifest.json**
```json
{
  "dependencies": {
    "com.unityvision.bridge": "https://github.com/nicweberdev/UnityVision.git?path=/Packages/com.unityvision.bridge"
  }
}
```

**Option C: Local file reference (for development)**
```json
{
  "dependencies": {
    "com.unityvision.bridge": "file:../path/to/UnityVision/Packages/com.unityvision.bridge"
  }
}
```

### Step 3: Build the MCP Server

```bash
cd unity-mcp-server
npm install
npm run build
```

### Step 4: Configure Your AI Client

You need to tell your AI client where to find the MCP server. The easiest way is to use the **built-in config generator** in Unity:

1. Open Unity with the package installed
2. Go to `Window > UnityVision > Bridge Status`
3. Expand **"AI Client Configuration"**
4. Click **"Copy Config"**
5. Paste into your AI client's MCP config file

#### Manual Configuration

If you prefer manual setup, add this to your AI client's MCP config:

```json
{
  "mcpServers": {
    "unity-vision": {
      "command": "node",
      "args": ["/absolute/path/to/UnityVision/unity-mcp-server/dist/server.js"]
    }
  }
}
```

> ⚠️ **Important:** Use forward slashes `/` even on Windows. Backslashes may cause issues.

---

<details>
<summary><b>🌊 Windsurf / Cascade</b></summary>

**Config file location:**
- Windows: `C:\Users\<username>\.codeium\windsurf\mcp_config.json`
- macOS: `~/.codeium/windsurf/mcp_config.json`
- Linux: `~/.codeium/windsurf/mcp_config.json`

**Add this to the `mcpServers` object:**

```json
"unity-vision": {
  "command": "node",
  "args": ["/path/to/UnityVision/unity-mcp-server/dist/server.js"]
}
```

After saving, **restart Windsurf** to load the new MCP server.

</details>

<details>
<summary><b>🤖 Claude Desktop</b></summary>

**Config file location:**
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`

**Add to the config:**

```json
{
  "mcpServers": {
    "unity-vision": {
      "command": "node",
      "args": ["/absolute/path/to/UnityVision/unity-mcp-server/dist/server.js"]
    }
  }
}
```

Restart Claude Desktop after saving.

</details>

<details>
<summary><b>📝 Cursor</b></summary>

Open Cursor Settings → MCP Servers → Add new server with:

```json
{
  "unity-vision": {
    "command": "node",
    "args": ["/absolute/path/to/UnityVision/unity-mcp-server/dist/server.js"]
  }
}
```

</details>

---

#### Troubleshooting Configuration

| Issue | Solution |
|-------|----------|
| "Cannot find module" | Check the path is correct and you ran `npm run build` |
| Server not appearing | Restart your AI client after config changes |
| Connection refused | Make sure Unity is open with the bridge running |
| Port conflicts | UnityVision auto-assigns ports 7890-7899, no manual config needed |

---

## 🎯 Quick Start

1. **Open Unity** - The bridge server starts automatically
2. **Verify connection** - Go to `Window > UnityVision > Bridge Status`
3. **Start your AI client** - It will connect via MCP
4. **Ask the AI to interact with Unity!**

```
"Show me the current scene hierarchy"
"Create a cube at position (0, 5, 0) with a Rigidbody"
"What errors are in the console?"
"Take a screenshot of the game view"
```

---

## 🛠️ Available Tools (26+)

Tools are consolidated into logical groups using an `action` parameter. This keeps the tool count low while providing full functionality.

| Tool | Actions | Description |
|------|---------|-------------|
| `unity_editor` | `get_state`, `set_play_mode`, `get_context`, `recompile`, `refresh` | Editor state, play mode, recompile scripts |
| `unity_console` | `get_logs`, `clear` | Console log management |
| `unity_scene` | `list`, `hierarchy`, `create`, `load`, `save`, `delete` | Scene management and CRUD |
| `unity_gameobject` | `create`, `modify`, `delete` | GameObject CRUD operations |
| `unity_component` | `search`, `add`, `set_properties`, `get_properties`, `set_property`, `compare` | Component management |
| `unity_selection` | `get`, `set` | Editor selection |
| `unity_asset` | `search`, `create_folder`, `move`, `delete`, `get_info`, `create_prefab`, `instantiate_prefab` | Asset management |
| `unity_material` | `get_properties`, `set_property`, `list`, `list_shaders` | Material and shader management |
| `unity_prefab` | `get_overrides`, `apply`, `revert`, `find_instances` | Prefab workflow |
| `unity_query` | `find_by_component`, `find_missing_refs`, `analyze_layers`, `find_in_radius` | Scene queries |
| `unity_dependency` | `find_references`, `get_dependencies`, `find_unused` | Asset dependency analysis |
| `unity_animation` | `get_state`, `set_parameter`, `get_clips`, `play`, `sample` | Animation control |
| `unity_audio` | `list_sources`, `get_clip_info`, `list_clips`, `preview`, `set_source` | Audio management |
| `unity_profiler` | `rendering_stats`, `memory_snapshot`, `recommendations` | Performance profiling |
| `unity_screenshot` | `game_view`, `scene_view` | Screenshot capture |
| `unity_xr` | `set_pose`, `teleport` | XR/VR control |
| `unity_shadergraph` | `get_info`, `list`, `create`, `list_node_types` | ShaderGraph management |
| `unity_ui` | `dump_layout` | UI hierarchy inspection |
| `unity_menu` | `execute`, `list` | Menu item execution |
| `unity_code` | `execute`, `evaluate` | C# code execution |
| `unity_test` | `run` | Test runner |
| `unity_build` | `player` | Build player |
| `unity_project` | `list`, `switch`, `get_active` | Multi-project management |
| `unity_batch` | `execute` | Batch operations |
| `unity_script` | `create`, `read`, `update`, `delete`, `validate`, `get_sha`, `apply_text_edits` | C# script management with atomic writes |
| `unity_package` | `list`, `add`, `remove` | Unity Package Manager |

### New in v1.1 (Phase 50)

| Tool/Feature | Description |
|--------------|-------------|
| `get_compilation_status` | Get async recompilation status with detailed error/warning messages |
| `get_console_logs_detailed` | Console logs with file path and line number via reflection |
| `apply_text_edits` | Line-based script edits with SHA256 precondition for conflict detection |
| Auto-Create Hierarchy | `FindGameObjectByPath("A/B/C", autoCreate: true)` creates missing parents |
| Safe Tag Assignment | `SetTagSafe(go, "Enemy", autoCreate: true)` creates missing tags |
| Atomic File Writes | Scripts written to `.tmp` then atomically moved to prevent corruption |
| Path Traversal Protection | All file operations validated to stay within Assets/ or Packages/ |

### Custom Tools (Extensible)

UnityVision supports **custom tool registration**. Unity projects can define their own tools that are automatically discovered and registered with the MCP server:

| Custom Tool | Description |
|-------------|-------------|
| `unity_ping` | Test connection and custom tool registration |
| `unity_run_tests` | Run Unity Test Framework tests (EditMode/PlayMode) |
| `unity_list_tests` | List all available Unity tests |

**Creating Custom Tools:** Inherit from `McpToolBase` and implement `Execute()` or `ExecuteAsync()`. Tools are auto-discovered via reflection.

### Usage Examples

```javascript
// Get editor state
unity_editor({ action: 'get_state' })

// Enter play mode
unity_editor({ action: 'set_play_mode', mode: 'play' })

// Get console errors
unity_console({ action: 'get_logs', logType: 'error' })

// Create a cube
unity_gameobject({ action: 'create', name: 'MyCube', primitiveType: 'Cube' })

// Search for materials
unity_asset({ action: 'search', searchQuery: 't:Material' })

// Take a screenshot
unity_screenshot({ action: 'game_view' })

// Create a new script
unity_script({ action: 'create', path: 'Scripts/PlayerController.cs', template: 'MonoBehaviour' })

// List installed packages
unity_package({ action: 'list' })

// Recompile scripts
unity_editor({ action: 'recompile' })
```

---

## 💡 Example Workflows

### 🐛 Debugging a NullReferenceException

```
User: "My game crashes with a NullReferenceException when I press Play"

AI uses:
1. unity_console({ action: 'get_logs', logType: 'error' }) → Sees the exception with stack trace
2. Reads the script file mentioned in the stack trace
3. Identifies the uninitialized variable
4. Fixes the code
5. unity_editor({ action: 'set_play_mode', mode: 'play' }) → Tests the fix
6. unity_console({ action: 'get_logs', logType: 'error' }) → Confirms no more errors
```

### 🎨 Setting Up a UI Layout

```
User: "Create a main menu with Play and Quit buttons"

AI uses:
1. unity_menu({ action: 'execute', menuPath: 'GameObject/UI/Canvas' }) → Creates Canvas
2. unity_gameobject({ action: 'create', ... }) → Creates buttons
3. unity_component({ action: 'set_properties', ... }) → Configures button text
4. unity_screenshot({ action: 'game_view' }) → Shows the result to verify
```

### 🏗️ Batch Scene Setup

```
User: "Create 10 cubes in a circle pattern"

AI uses:
1. unity_batch({ action: 'execute', operations: [...] })
   - 10 gameobject create operations with calculated positions
   - Atomic rollback if any fails
```

### 🥽 VR Development

```
User: "Position the XR rig at the spawn point and take a screenshot"

AI uses:
1. unity_scene({ action: 'hierarchy' }) → Finds the spawn point transform
2. unity_xr({ action: 'set_pose', position: {...} }) → Moves rig to spawn
3. unity_screenshot({ action: 'game_view' }) → Shows VR perspective
```

### 🔀 Multi-Project Workflow

```
User: "I have two Unity projects open - switch to MyGame and show its hierarchy"

AI uses:
1. unity_project({ action: 'list' }) → Sees both projects
2. unity_project({ action: 'switch', projectPath: 'MyGame' }) → Changes target
3. unity_scene({ action: 'hierarchy' }) → Shows hierarchy of MyGame
```

---

## 🔒 Safety Features

UnityVision is designed with safety in mind:

- **↩️ Full Undo Support** - Every mutation registers with Unity's Undo system. Press Ctrl+Z to revert any AI action.
- **🔍 Dry-Run Mode** - Pass `dryRun: true` to preview what would change without actually changing it.
- **⚠️ Confirmation Required** - Destructive operations like `delete_game_object` and `delete_asset` require explicit `confirm: true`.
- **🏠 Localhost Only** - The bridge server only binds to `localhost`. No remote access possible.
- **🔐 Optional Auth** - Enable session token authentication with `UNITY_VISION_REQUIRE_AUTH=true`.
- **📁 Path Traversal Protection** - All file operations validate paths stay within `Assets/` or `Packages/`. Rejects `..` traversal and symlinks.
- **💾 Atomic File Writes** - Scripts written to `.tmp` file first, then atomically moved to prevent corruption on crash/interrupt.
- **🔑 SHA256 Preconditions** - Script updates can require `precondition_sha256` to detect conflicts from concurrent edits.

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    AI Assistant (Claude, etc.)                  │
│                          MCP Client                             │
└─────────────────────────────────────────────────────────────────┘
                                │
                                │ stdio (MCP Protocol)
                                ▼
┌─────────────────────────────────────────────────────────────────┐
│                    UnityVision MCP Server                       │
│                     (Node.js + TypeScript)                      │
│                                                                 │
│  • 26 Consolidated tools + 5 MCP Resources                      │
│  • WebSocket Server (port 7890)                                 │
│  • Custom tool registration from Unity                          │
│  • Type-safe request/response handling                          │
└─────────────────────────────────────────────────────────────────┘
                                ▲
                                │ WebSocket ws://localhost:7890
                                │ (Unity connects to MCP server)
                                │
┌─────────────────────────────────────────────────────────────────┐
│                    Unity Editor(s)                              │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │           UnityVision Bridge (C# Package)               │   │
│  │                                                         │   │
│  │  • WebSocket CLIENT (connects to MCP server)            │   │
│  │  • Auto-reconnect with exponential backoff              │   │
│  │  • Custom tool registration (register_tools)            │   │
│  │  • Main thread dispatch for Unity API calls             │   │
│  │  • Play mode & assembly reload handling                 │   │
│  │  • Full Undo integration                                │   │
│  │  • Atomic file writes with path protection              │   │
│  │  • Custom JSON converters for Unity types               │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📁 Project Structure

```
UnityVision/
├── unity-mcp-server/              # Node.js MCP server
│   ├── src/
│   │   ├── server.ts              # MCP entry point
│   │   ├── websocketHub.ts        # WebSocket server for Unity connections
│   │   ├── unityBridgeClient.ts   # Bridge client for tool execution
│   │   ├── types.ts               # TypeScript type definitions
│   │   ├── resources/             # MCP Resources
│   │   │   └── index.ts           # 5 resources (hierarchy, selection, logs, etc.)
│   │   └── tools/                 # Tool implementations
│   │       ├── consolidatedTools.ts  # 26 grouped tools with action parameter
│   │       ├── index.ts              # Tool registry
│   │       └── ...                   # Handler files
│   ├── tests/                     # Jest unit tests (102 tests)
│   ├── package.json
│   └── tsconfig.json
│
├── Packages/
│   └── com.unityvision.bridge/    # Unity package
│       ├── Editor/
│       │   ├── Bridge/
│       │   │   ├── BridgeServer.cs    # HTTP server (legacy, for fallback)
│       │   │   ├── RpcHandler.cs      # Method dispatch
│       │   │   └── RpcRequest.cs      # Request/response types
│       │   ├── Transport/
│       │   │   └── WebSocketClient.cs # WebSocket client (connects to MCP)
│       │   ├── Serialization/         # Custom JSON converters (NEW)
│       │   │   └── UnityJsonConverters.cs # Vector3, Color, Quaternion, etc.
│       │   ├── Tools/                 # Custom tool system
│       │   │   ├── McpToolBase.cs     # Base class for custom tools
│       │   │   ├── ToolRegistry.cs    # Auto-discovery and registration
│       │   │   └── CustomTools/       # Built-in custom tools
│       │   ├── Services/
│       │   │   └── TestRunnerService.cs  # Unity Test Framework integration
│       │   ├── Utils/
│       │   │   ├── McpServerSetup.cs     # Auto npm install/build
│       │   │   └── MainThreadDispatcher.cs # Main thread dispatch
│       │   ├── Handlers/              # 15+ handler files
│       │   │   ├── EditorHandlers.cs     # Editor state, recompile, compilation status
│       │   │   ├── SceneHandlers.cs      # Scene CRUD operations
│       │   │   ├── GameObjectHandlers.cs # Auto-create hierarchy, safe tags
│       │   │   ├── ScriptHandler.cs      # Atomic writes, path protection
│       │   │   ├── ConsoleHandlers.cs    # File/line info via reflection
│       │   │   └── ...
│       │   └── UI/
│       │       └── BridgeStatusWindow.cs  # Status UI with connection status
│       └── package.json
│
└── README.md
```

---

## ⚙️ Configuration

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_VISION_PORT` | Auto (7890-7899) | Override auto-port with specific port |
| `UNITY_VISION_REQUIRE_AUTH` | `false` | Enable session token authentication |

### Multi-Project Support

UnityVision automatically handles multiple Unity instances:

1. **Auto-Port Assignment** - Each Unity instance gets a unique port (7890-7899)
2. **Project Registry** - Projects register at `~/.unityvision/projects.json`
3. **Auto-Discovery** - MCP server discovers all running projects
4. **Smart Routing** - If one project is open, it's auto-selected. Multiple projects prompt for selection.

### Manual Port Override

If you need a specific port (e.g., for firewall rules):

1. Set `UNITY_VISION_PORT` environment variable before starting Unity
2. Or the MCP server will auto-discover whatever port Unity chose

---

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Setup

```bash
# Clone your fork
git clone https://github.com/YOUR_USERNAME/UnityVision.git

# Install dependencies
cd UnityVision/unity-mcp-server
npm install

# Build in watch mode
npm run build -- --watch
```

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- [Model Context Protocol](https://modelcontextprotocol.io/) for the MCP specification
- [Unity Technologies](https://unity.com/) for the amazing game engine
- The AI assistant community for pushing the boundaries of what's possible

---

<div align="center">

**Built with ❤️ for the Unity + AI community**

*Status as of December 8, 2025*

[⬆ Back to top](#-unityvision-mcp)

</div>
