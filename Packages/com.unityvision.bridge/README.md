# UnityVision Bridge

Unity Editor package that provides an HTTP bridge for the UnityVision MCP Server.

## Installation

### Option 1: Local Package (Recommended for Development)

1. Copy the `com.unityvision.bridge` folder to your Unity project's `Packages` folder
2. Or add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.unityvision.bridge": "file:../path/to/com.unityvision.bridge"
  }
}
```

### Option 2: Git URL

```json
{
  "dependencies": {
    "com.unityvision.bridge": "https://github.com/yourcompany/unity-vision.git?path=Packages/com.unityvision.bridge"
  }
}
```

## Requirements

- Unity 2021.3 or later
- Newtonsoft.Json (automatically installed as dependency)

## Usage

The bridge server starts automatically when Unity Editor loads. No configuration required.

### Check Status

Open **Window > UnityVision > Bridge Status** to see:
- Server running status
- Port number
- Request count
- Last error (if any)

### Default Port

The server runs on `localhost:7890` by default.

To change the port, set the `UNITY_VISION_PORT` environment variable before starting Unity.

## API Endpoints

### Health Check

```
GET http://localhost:7890/health
```

Returns:
```json
{
  "status": "ok",
  "port": 7890
}
```

### RPC Endpoint

```
POST http://localhost:7890/rpc
Content-Type: application/json

{
  "method": "get_editor_state",
  "params": {}
}
```

## Available Methods

### Editor
- `get_editor_state` - Unity version, project path, play mode
- `set_play_mode` - Control play/pause/stop

### Console
- `get_console_logs` - Fetch logs with filters
- `clear_console_logs` - Clear console

### Scenes
- `list_scenes` - List build scenes
- `get_scene_hierarchy` - GameObject tree

### GameObjects
- `create_game_object` - Create with components
- `modify_game_object` - Transform, rename, reparent
- `delete_game_object` - Delete with confirmation

### Components
- `add_component` - Add by type name
- `set_component_properties` - Set serialized fields

### UI
- `dump_ui_layout` - RectTransform hierarchy

### Screenshots
- `capture_game_view_screenshot` - Game camera view
- `capture_scene_view_screenshot` - Scene editor view

### XR
- `set_xr_rig_pose` - Position XR rig
- `teleport_xr_rig_to_anchor` - Teleport to anchor

### Tests
- `run_tests` - Run Unity tests

### Builds
- `build_player` - Build for platform

## Undo Support

All mutating operations use Unity's Undo system. Press Ctrl+Z to undo any changes made via the bridge.

## Dry Run Mode

Add `"dryRun": true` to any mutating request to preview changes without applying them.

## Security

The server only binds to `localhost` and is not accessible from other machines. This is intentional for security.

## Troubleshooting

### Server Not Starting

1. Check if port 7890 is already in use
2. Look for errors in Unity Console
3. Try restarting via Window > UnityVision > Bridge Status

### Requests Timing Out

1. Ensure Unity Editor is not blocked (e.g., by a modal dialog)
2. Check if the operation is taking too long
3. Increase timeout in MCP server configuration

### Log Capture Not Working

The log buffer starts capturing when the package loads. Logs from before package initialization are not captured.

## License

MIT
