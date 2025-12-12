// ============================================================================
// UnityVision Bridge - Status Window
// Enhanced editor window for monitoring bridge server status and activity
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;
using UnityVision.Editor.Registry;
using UnityVision.Editor.Transport;

namespace UnityVision.Editor.UI
{
    public class BridgeStatusWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private Vector2 _activityScrollPosition;
        private GUIStyle _statusStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _activityItemStyle;
        private GUIStyle _successStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _connectedStyle;
        private GUIStyle _waitingStyle;
        private GUIStyle _configuredStyle;
        private GUIStyle _notConfiguredStyle;
        
        private bool _showActivity = true;
        private bool _showIDEConfig = true;
        
        private string _mcpServerPath = "";
        private List<IDEInfo> _detectedIDEs = new List<IDEInfo>();
        
        private double _lastRepaintTime;
        private const float REPAINT_INTERVAL = 0.5f;
        private const string MCP_PATH_PREF_KEY = "UnityVision_McpServerPath";

        [MenuItem("Window/UnityVision/Bridge Status")]
        public static void ShowWindow()
        {
            var window = GetWindow<BridgeStatusWindow>("UnityVision Bridge");
            window.minSize = new Vector2(400, 450);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnUpdate;
            LoadMcpServerPath();
            DetectIDEs();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
        }

        private void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastRepaintTime > REPAINT_INTERVAL)
            {
                _lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void InitStyles()
        {
            if (_statusStyle == null)
            {
                _statusStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12,
                    padding = new RectOffset(5, 5, 2, 2)
                };
            }

            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    padding = new RectOffset(5, 5, 5, 5)
                };
            }

            if (_subHeaderStyle == null)
            {
                _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    padding = new RectOffset(0, 0, 2, 2)
                };
            }

            if (_activityItemStyle == null)
            {
                _activityItemStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    padding = new RectOffset(2, 2, 1, 1)
                };
            }

            // Bright green for success - much more visible
            if (_successStyle == null)
            {
                _successStyle = new GUIStyle(_activityItemStyle);
                _successStyle.normal.textColor = new Color(0.3f, 0.9f, 0.3f);
            }

            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(_activityItemStyle);
                _errorStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
            }

            // Connected status - bright green
            if (_connectedStyle == null)
            {
                _connectedStyle = new GUIStyle(EditorStyles.boldLabel);
                _connectedStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
                _connectedStyle.fontSize = 14;
                _connectedStyle.alignment = TextAnchor.MiddleCenter;
            }

            // Waiting status - yellow/amber
            if (_waitingStyle == null)
            {
                _waitingStyle = new GUIStyle(EditorStyles.boldLabel);
                _waitingStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
                _waitingStyle.fontSize = 14;
                _waitingStyle.alignment = TextAnchor.MiddleCenter;
            }

            // Configured IDE - bright green
            if (_configuredStyle == null)
            {
                _configuredStyle = new GUIStyle(EditorStyles.label);
                _configuredStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
            }

            // Not configured IDE - yellow/orange
            if (_notConfiguredStyle == null)
            {
                _notConfiguredStyle = new GUIStyle(EditorStyles.label);
                _notConfiguredStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
            }
        }

        private void OnGUI()
        {
            InitStyles();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🔗 UnityVision MCP Bridge", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("🩺", GUILayout.Width(28), GUILayout.Height(25)))
            {
                DiagnosticsPanel.ShowWindow();
            }
            if (GUILayout.Button("?", GUILayout.Width(25), GUILayout.Height(25)))
            {
                Application.OpenURL("https://github.com/nicweberdev/UnityVision");
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);

            // Main status card
            DrawStatusCard();

            EditorGUILayout.Space(10);

            // Server controls
            DrawServerControls();

            EditorGUILayout.Space(10);

            // MCP Server Path section
            DrawMcpServerPathSection();

            EditorGUILayout.Space(10);

            // IDE Configuration section (collapsible)
            _showIDEConfig = EditorGUILayout.Foldout(_showIDEConfig, "🖥️ AI Client Auto-Configuration", true);
            if (_showIDEConfig)
            {
                DrawIDEConfigSection();
            }

            EditorGUILayout.Space(5);

            // Activity log (collapsible)
            _showActivity = EditorGUILayout.Foldout(_showActivity, "📊 Recent Activity", true);
            if (_showActivity)
            {
                DrawActivityLog();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawStatusCard()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Status row with large indicator
            EditorGUILayout.BeginHorizontal();
            
            // Status indicator - shows WebSocket connection to MCP server
            // "CONNECTED" = WebSocket connected to MCP server (Windsurf)
            // "CONNECTING" = Attempting to connect
            // "DISCONNECTED" = Not connected to MCP server
            if (WebSocketClient.IsConnected)
            {
                EditorGUILayout.LabelField("● CONNECTED", _connectedStyle, GUILayout.Width(130), GUILayout.Height(24));
            }
            else
            {
                // Not connected - show yellow "WAITING" state
                EditorGUILayout.LabelField("● WAITING", _waitingStyle, GUILayout.Width(110), GUILayout.Height(24));
            }
            
            GUILayout.FlexibleSpace();
            
            // Port info
            string portInfo = $"Port {WebSocketClient.Port}";
            EditorGUILayout.LabelField(portInfo, EditorStyles.boldLabel, GUILayout.Width(100));
            
            EditorGUILayout.EndHorizontal();
            
            // Connection details
            if (WebSocketClient.IsConnected)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Session: {WebSocketClient.SessionId?.Substring(0, 8) ?? "..."}...", EditorStyles.miniLabel);
                if (WebSocketClient.ConnectedSince.HasValue)
                {
                    EditorGUILayout.LabelField($"Connected {GetRelativeTime(WebSocketClient.ConnectedSince.Value)}", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Waiting for MCP server (Windsurf)...", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(5);

            // Stats row
            EditorGUILayout.BeginHorizontal();
            
            // Request count
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("Requests", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(BridgeConfig.RequestCount.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            
            // Last request time
            EditorGUILayout.BeginVertical(GUILayout.Width(120));
            EditorGUILayout.LabelField("Last Request", EditorStyles.miniLabel);
            string lastTime = BridgeConfig.LastRequestTime.HasValue 
                ? GetRelativeTime(BridgeConfig.LastRequestTime.Value) 
                : "Never";
            EditorGUILayout.LabelField(lastTime, EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            
            // Auth status
            EditorGUILayout.BeginVertical(GUILayout.Width(80));
            EditorGUILayout.LabelField("Auth", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(BridgeConfig.RequireAuth ? "Required" : "Disabled", EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();

            // Error display removed - WebSocket client handles errors directly

            EditorGUILayout.EndVertical();
        }

        private void DrawServerControls()
        {
            EditorGUILayout.BeginHorizontal();

            if (WebSocketClient.IsConnected)
            {
                if (GUILayout.Button("⏹ Disconnect", GUILayout.Height(28)))
                {
                    WebSocketClient.Disconnect();
                }
            }
            else
            {
                if (GUILayout.Button("▶ Connect", GUILayout.Height(28)))
                {
                    WebSocketClient.ConnectAsync();
                }
            }

            if (GUILayout.Button("🔄 Reconnect", GUILayout.Height(28)))
            {
                WebSocketClient.Reconnect();
            }

            if (GUILayout.Button("📋 Copy Port", GUILayout.Height(28)))
            {
                EditorGUIUtility.systemCopyBuffer = WebSocketClient.Port.ToString();
                Debug.Log($"[UnityVision] Port {WebSocketClient.Port} copied to clipboard");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawActivityLog()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var activity = BridgeConfig.RecentActivity;
            
            if (activity.Count == 0)
            {
                EditorGUILayout.LabelField("No recent activity", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                _activityScrollPosition = EditorGUILayout.BeginScrollView(
                    _activityScrollPosition, 
                    GUILayout.Height(Mathf.Min(150, activity.Count * 20 + 10)));

                // Show last 10 requests
                int count = Mathf.Min(10, activity.Count);
                for (int i = 0; i < count; i++)
                {
                    var item = activity[i];
                    DrawActivityItem(item);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActivityItem(RequestActivity item)
        {
            EditorGUILayout.BeginHorizontal();
            
            // Status icon
            var style = item.Success ? _successStyle : _errorStyle;
            string icon = item.Success ? "✓" : "✗";
            EditorGUILayout.LabelField(icon, style, GUILayout.Width(15));
            
            // Method name
            EditorGUILayout.LabelField(item.Method, _activityItemStyle, GUILayout.Width(150));
            
            // Duration
            EditorGUILayout.LabelField($"{item.DurationMs}ms", _activityItemStyle, GUILayout.Width(50));
            
            // Time
            string timeStr = GetRelativeTime(item.Timestamp);
            EditorGUILayout.LabelField(timeStr, _activityItemStyle);
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMcpServerPathSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("📁 MCP Server Path", _subHeaderStyle);
            
            EditorGUILayout.BeginHorizontal();
            
            bool pathValid = !string.IsNullOrEmpty(_mcpServerPath) && File.Exists(Path.Combine(_mcpServerPath, "dist", "server.js"));
            
            _mcpServerPath = EditorGUILayout.TextField(_mcpServerPath, GUILayout.ExpandWidth(true));
            
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select unity-mcp-server folder", _mcpServerPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (File.Exists(Path.Combine(selectedPath, "dist", "server.js")))
                    {
                        _mcpServerPath = selectedPath;
                        SaveMcpServerPath();
                        DetectIDEs();
                    }
                    else if (File.Exists(Path.Combine(selectedPath, "unity-mcp-server", "dist", "server.js")))
                    {
                        _mcpServerPath = Path.Combine(selectedPath, "unity-mcp-server");
                        SaveMcpServerPath();
                        DetectIDEs();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Path", 
                            "Could not find 'dist/server.js'.\n\n" +
                            "Select the 'unity-mcp-server' folder and run 'npm run build' first.", "OK");
                    }
                }
            }
            
            if (GUILayout.Button("Auto", GUILayout.Width(45)))
            {
                AutoDetectMcpServerPath();
                DetectIDEs();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Status
            if (pathValid)
            {
                EditorGUILayout.LabelField("✓ Server ready", _configuredStyle);
            }
            else if (string.IsNullOrEmpty(_mcpServerPath))
            {
                EditorGUILayout.LabelField("⚠ Set path to enable auto-configuration", _notConfiguredStyle);
            }
            else
            {
                EditorGUILayout.LabelField("✗ Run 'npm run build' first", _errorStyle);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawIDEConfigSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_detectedIDEs.Count == 0)
            {
                EditorGUILayout.LabelField("No supported AI clients detected.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            bool pathValid = !string.IsNullOrEmpty(_mcpServerPath) && File.Exists(Path.Combine(_mcpServerPath, "dist", "server.js"));

            foreach (var ide in _detectedIDEs)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Status icon and name
                string statusIcon = ide.IsConfigured ? "✓" : (ide.ConfigExists ? "○" : "○");
                GUIStyle statusStyle = ide.IsConfigured ? _configuredStyle : _notConfiguredStyle;
                string statusText = ide.IsConfigured ? "Ready" : "Not configured";
                
                EditorGUILayout.LabelField(statusIcon, statusStyle, GUILayout.Width(15));
                EditorGUILayout.LabelField(ide.Name, EditorStyles.boldLabel, GUILayout.Width(110));
                EditorGUILayout.LabelField(statusText, GUILayout.ExpandWidth(true));
                
                // Configure button
                GUI.enabled = pathValid;
                string buttonText = ide.IsConfigured ? "Update" : "Configure";
                if (GUILayout.Button(buttonText, GUILayout.Width(70)))
                {
                    ConfigureIDE(ide);
                }
                GUI.enabled = true;
                
                // Open folder button
                if (GUILayout.Button("📂", GUILayout.Width(28)))
                {
                    string configDir = Path.GetDirectoryName(ide.ConfigPath);
                    if (Directory.Exists(configDir))
                    {
                        EditorUtility.RevealInFinder(ide.ConfigPath);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (!pathValid)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Set MCP Server path above to enable configuration", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private string GetRelativeTime(DateTime time)
        {
            var diff = DateTime.Now - time;
            
            if (diff.TotalSeconds < 5) return "just now";
            if (diff.TotalSeconds < 60) return $"{(int)diff.TotalSeconds}s ago";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return time.ToString("MMM dd HH:mm");
        }

        // =========================================================================
        // IDE Detection and Configuration
        // =========================================================================

        private void LoadMcpServerPath()
        {
            _mcpServerPath = EditorPrefs.GetString(MCP_PATH_PREF_KEY, "");
            if (string.IsNullOrEmpty(_mcpServerPath))
            {
                AutoDetectMcpServerPath();
            }
        }

        private void SaveMcpServerPath()
        {
            EditorPrefs.SetString(MCP_PATH_PREF_KEY, _mcpServerPath);
        }

        private void AutoDetectMcpServerPath()
        {
            string projectPath = Application.dataPath;
            string[] searchPaths = new[]
            {
                Path.GetFullPath(Path.Combine(projectPath, "..", "unity-mcp-server")),
                Path.GetFullPath(Path.Combine(projectPath, "..", "..", "unity-mcp-server")),
                Path.GetFullPath(Path.Combine(projectPath, "..", "UnityVision", "unity-mcp-server")),
                Path.GetFullPath(Path.Combine(projectPath, "..", "..", "UnityVision", "unity-mcp-server")),
            };

            foreach (var searchPath in searchPaths)
            {
                if (File.Exists(Path.Combine(searchPath, "dist", "server.js")))
                {
                    _mcpServerPath = searchPath;
                    SaveMcpServerPath();
                    return;
                }
            }

            // Check if source exists but not built
            foreach (var searchPath in searchPaths)
            {
                if (File.Exists(Path.Combine(searchPath, "src", "server.ts")))
                {
                    _mcpServerPath = searchPath;
                    SaveMcpServerPath();
                    return;
                }
            }
        }

        private void DetectIDEs()
        {
            _detectedIDEs.Clear();
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Windsurf
            string[] windsurfPaths = new[]
            {
                Path.Combine(home, ".codeium", "windsurf", "mcp_config.json"),
                Path.Combine(home, "AppData", "Roaming", "Windsurf", "mcp_config.json"),
            };
            foreach (var path in windsurfPaths)
            {
                if (File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path)))
                {
                    _detectedIDEs.Add(new IDEInfo
                    {
                        Name = "Windsurf",
                        ConfigPath = path,
                        ConfigKey = "mcpServers",
                        ConfigExists = File.Exists(path),
                        IsConfigured = CheckIDEConfigured(path)
                    });
                    break;
                }
            }

            // Claude Desktop
            string[] claudePaths = new[]
            {
                Path.Combine(home, "AppData", "Roaming", "Claude", "claude_desktop_config.json"),
                Path.Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json"),
                Path.Combine(home, ".config", "claude", "claude_desktop_config.json"),
            };
            foreach (var path in claudePaths)
            {
                if (File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path)))
                {
                    _detectedIDEs.Add(new IDEInfo
                    {
                        Name = "Claude Desktop",
                        ConfigPath = path,
                        ConfigKey = "mcpServers",
                        ConfigExists = File.Exists(path),
                        IsConfigured = CheckIDEConfigured(path)
                    });
                    break;
                }
            }

            // Cursor
            string[] cursorPaths = new[]
            {
                Path.Combine(home, ".cursor", "mcp.json"),
                Path.Combine(home, "AppData", "Roaming", "Cursor", "mcp.json"),
            };
            foreach (var path in cursorPaths)
            {
                if (File.Exists(path) || Directory.Exists(Path.GetDirectoryName(path)))
                {
                    _detectedIDEs.Add(new IDEInfo
                    {
                        Name = "Cursor",
                        ConfigPath = path,
                        ConfigKey = "mcpServers",
                        ConfigExists = File.Exists(path),
                        IsConfigured = CheckIDEConfigured(path)
                    });
                    break;
                }
            }
        }

        private bool CheckIDEConfigured(string configPath)
        {
            if (!File.Exists(configPath)) return false;
            try
            {
                string content = File.ReadAllText(configPath);
                return content.Contains("unity-vision") || content.Contains("unityvision");
            }
            catch
            {
                return false;
            }
        }

        private void ConfigureIDE(IDEInfo ide)
        {
            string serverJsPath = Path.Combine(_mcpServerPath, "dist", "server.js").Replace("\\", "/");

            try
            {
                // Ensure directory exists
                string configDir = Path.GetDirectoryName(ide.ConfigPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                // Read existing config or create new
                Dictionary<string, object> config;
                if (File.Exists(ide.ConfigPath))
                {
                    string backupPath = ide.ConfigPath + ".backup";
                    File.Copy(ide.ConfigPath, backupPath, true);
                    
                    string content = File.ReadAllText(ide.ConfigPath);
                    config = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(content) 
                             ?? new Dictionary<string, object>();
                }
                else
                {
                    config = new Dictionary<string, object>();
                }

                // Ensure mcpServers exists
                if (!config.ContainsKey(ide.ConfigKey))
                {
                    config[ide.ConfigKey] = new Dictionary<string, object>();
                }

                var mcpServers = config[ide.ConfigKey] as Newtonsoft.Json.Linq.JObject;
                if (mcpServers == null)
                {
                    mcpServers = new Newtonsoft.Json.Linq.JObject();
                    config[ide.ConfigKey] = mcpServers;
                }

                // Add/update unity-vision entry
                mcpServers["unity-vision"] = Newtonsoft.Json.Linq.JObject.FromObject(new
                {
                    command = "node",
                    args = new[] { serverJsPath }
                });

                // Write config
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(ide.ConfigPath, json);

                // Refresh
                DetectIDEs();

                EditorUtility.DisplayDialog("Success", 
                    $"UnityVision configured for {ide.Name}!\n\n" +
                    "Restart your AI client to apply changes.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to configure {ide.Name}:\n{ex.Message}", "OK");
            }
        }
    }

    // IDE Info class for BridgeStatusWindow
    public class IDEInfo
    {
        public string Name { get; set; }
        public string ConfigPath { get; set; }
        public string ConfigKey { get; set; }
        public bool ConfigExists { get; set; }
        public bool IsConfigured { get; set; }
    }
}
