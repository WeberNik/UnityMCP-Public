// ============================================================================
// UnityVision Bridge - Diagnostics Panel
// Self-diagnostic checks and troubleshooting
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;
using UnityVision.Editor.Transport;
using Debug = UnityEngine.Debug;

namespace UnityVision.Editor.UI
{
    public class DiagnosticsPanel : EditorWindow
    {
        private Vector2 _scrollPosition;
        private List<DiagnosticResult> _results = new List<DiagnosticResult>();
        private bool _isRunning = false;
        private bool _pendingRefresh = false;
        
        private GUIStyle _headerStyle;
        private GUIStyle _passStyle;
        private GUIStyle _failStyle;
        private GUIStyle _warnStyle;

        [MenuItem("Window/UnityVision/Diagnostics")]
        public static void ShowWindow()
        {
            var window = GetWindow<DiagnosticsPanel>("UnityVision Diagnostics");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            RunDiagnostics();
        }

        private void InitStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    padding = new RectOffset(5, 5, 5, 5)
                };
            }

            // Bright green - more visible
            if (_passStyle == null)
            {
                _passStyle = new GUIStyle(EditorStyles.label);
                _passStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
            }

            // Bright red
            if (_failStyle == null)
            {
                _failStyle = new GUIStyle(EditorStyles.label);
                _failStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
            }

            // Bright yellow/orange
            if (_warnStyle == null)
            {
                _warnStyle = new GUIStyle(EditorStyles.label);
                _warnStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
            }
        }

        private void OnGUI()
        {
            InitStyles();
            
            // Handle pending refresh (deferred to avoid collection modification during iteration)
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                RunDiagnostics();
            }

            EditorGUILayout.LabelField("🩺 UnityVision Diagnostics", _headerStyle);
            EditorGUILayout.Space(5);

            // Top buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 Run Diagnostics", GUILayout.Height(28)))
            {
                RunDiagnostics();
            }
            if (GUILayout.Button("📋 Copy Report", GUILayout.Height(28)))
            {
                CopyReport();
            }
            if (GUILayout.Button("📤 Export Logs", GUILayout.Height(28)))
            {
                ExportLogs();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (_isRunning)
            {
                EditorGUILayout.LabelField("Running diagnostics...", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Create a copy to avoid modification during iteration
            var resultsCopy = _results.ToList();
            foreach (var result in resultsCopy)
            {
                DrawDiagnosticResult(result);
            }

            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("For IDE configuration, use Window > UnityVision > Bridge Status", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawDiagnosticResult(DiagnosticResult result)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            
            // Status icon
            GUIStyle statusStyle;
            string icon;
            switch (result.Status)
            {
                case DiagnosticStatus.Pass:
                    icon = "✓";
                    statusStyle = _passStyle;
                    break;
                case DiagnosticStatus.Fail:
                    icon = "✗";
                    statusStyle = _failStyle;
                    break;
                case DiagnosticStatus.Warning:
                    icon = "⚠";
                    statusStyle = _warnStyle;
                    break;
                default:
                    icon = "?";
                    statusStyle = EditorStyles.label;
                    break;
            }

            EditorGUILayout.LabelField(icon, statusStyle, GUILayout.Width(20));
            EditorGUILayout.LabelField(result.Name, EditorStyles.boldLabel);
            
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(result.Message))
            {
                EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedMiniLabel);
            }

            if (!string.IsNullOrEmpty(result.Solution) && result.Status != DiagnosticStatus.Pass)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"💡 {result.Solution}", EditorStyles.wordWrappedMiniLabel);
            }

            if (result.FixAction != null && result.Status == DiagnosticStatus.Fail)
            {
                EditorGUILayout.Space(2);
                if (GUILayout.Button("🔧 Fix", GUILayout.Width(60)))
                {
                    result.FixAction();
                    // Defer refresh to avoid collection modification during iteration
                    _pendingRefresh = true;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void RunDiagnostics()
        {
            _isRunning = true;
            _results.Clear();
            Repaint();

            // Check 1: Bridge Server Status
            _results.Add(CheckBridgeServer());

            // Check 2: Port Availability
            _results.Add(CheckPortAvailability());

            // Check 3: Project Registry
            _results.Add(CheckProjectRegistry());

            // Check 4: Node.js Installation
            _results.Add(CheckNodeJs());

            // Check 5: MCP Server Build
            _results.Add(CheckMcpServerBuild());

            // Check 6: Firewall (basic check)
            _results.Add(CheckLocalhost());

            _isRunning = false;
            Repaint();
        }

        // =========================================================================
        // Diagnostic Checks
        // =========================================================================

        private DiagnosticResult CheckBridgeServer()
        {
            // Check WebSocket connection (primary transport)
            if (WebSocketClient.IsConnected)
            {
                return new DiagnosticResult
                {
                    Name = "WebSocket Connection",
                    Status = DiagnosticStatus.Pass,
                    Message = $"Connected to MCP server on port {WebSocketClient.Port}"
                };
            }
            else
            {
                return new DiagnosticResult
                {
                    Name = "WebSocket Connection",
                    Status = DiagnosticStatus.Warning,
                    Message = "Waiting for MCP server (Windsurf)",
                    Solution = "Ensure Windsurf is running with unity-vision MCP enabled",
                    FixAction = () => WebSocketClient.Reconnect()
                };
            }
        }

        private DiagnosticResult CheckPortAvailability()
        {
            int port = WebSocketClient.Port;
            
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("localhost", port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                    
                    if (success && client.Connected)
                    {
                        client.EndConnect(result);
                        return new DiagnosticResult
                        {
                            Name = "Port Availability",
                            Status = DiagnosticStatus.Pass,
                            Message = $"Port {port} is accessible"
                        };
                    }
                }
            }
            catch
            {
                // Connection failed
            }

            return new DiagnosticResult
            {
                Name = "Port Availability",
                Status = DiagnosticStatus.Warning,
                Message = $"Port {port} not responding - MCP server may not be running",
                Solution = "Ensure Windsurf is running with unity-vision MCP enabled"
            };
        }

        private DiagnosticResult CheckProjectRegistry()
        {
            string registryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unityvision",
                "projects.json"
            );

            if (File.Exists(registryPath))
            {
                try
                {
                    string content = File.ReadAllText(registryPath);
                    var entries = JsonUtility.FromJson<ProjectEntryList>("{\"entries\":" + content + "}");
                    int count = entries?.entries?.Length ?? 0;
                    
                    return new DiagnosticResult
                    {
                        Name = "Project Registry",
                        Status = DiagnosticStatus.Pass,
                        Message = $"Registry exists with {count} project(s)"
                    };
                }
                catch (Exception ex)
                {
                    return new DiagnosticResult
                    {
                        Name = "Project Registry",
                        Status = DiagnosticStatus.Warning,
                        Message = $"Registry exists but may be corrupted: {ex.Message}",
                        Solution = "The registry will be recreated automatically"
                    };
                }
            }
            else
            {
                string dirPath = Path.GetDirectoryName(registryPath);
                bool dirExists = Directory.Exists(dirPath);
                
                return new DiagnosticResult
                {
                    Name = "Project Registry",
                    Status = DiagnosticStatus.Warning,
                    Message = dirExists 
                        ? "Registry file not found (will be created on server start)"
                        : "Registry directory not found",
                    Solution = "Start the bridge server to create the registry"
                };
            }
        }

        private DiagnosticResult CheckNodeJs()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000); // 5 second timeout

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        string version = output.Trim();
                        // Check if version is 18+
                        if (version.StartsWith("v"))
                        {
                            var versionNum = version.Substring(1).Split('.')[0];
                            if (int.TryParse(versionNum, out int major) && major >= 18)
                            {
                                return new DiagnosticResult
                                {
                                    Name = "Node.js",
                                    Status = DiagnosticStatus.Pass,
                                    Message = $"Node.js {version} installed"
                                };
                            }
                            else
                            {
                                return new DiagnosticResult
                                {
                                    Name = "Node.js",
                                    Status = DiagnosticStatus.Warning,
                                    Message = $"Node.js {version} installed (v18+ recommended)",
                                    Solution = "Update Node.js to version 18 or higher"
                                };
                            }
                        }
                    }
                }
            }
            catch
            {
                // Node not found
            }

            return new DiagnosticResult
            {
                Name = "Node.js",
                Status = DiagnosticStatus.Fail,
                Message = "Node.js not found in PATH",
                Solution = "Install Node.js 18+ from https://nodejs.org"
            };
        }

        private DiagnosticResult CheckMcpServerBuild()
        {
            // Try to find the MCP server relative to the Unity project
            string projectPath = Application.dataPath;
            string[] possiblePaths = new[]
            {
                Path.GetFullPath(Path.Combine(projectPath, "../unity-mcp-server/dist/server.js")),
                Path.GetFullPath(Path.Combine(projectPath, "../../unity-mcp-server/dist/server.js")),
                Path.GetFullPath(Path.Combine(projectPath, "../UnityMCP/unity-mcp-server/dist/server.js")),
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    var fileInfo = new FileInfo(path);
                    return new DiagnosticResult
                    {
                        Name = "MCP Server Build",
                        Status = DiagnosticStatus.Pass,
                        Message = $"Found at {Path.GetDirectoryName(path)}"
                    };
                }
            }

            // Check if source exists but not built
            foreach (var path in possiblePaths)
            {
                string srcPath = path.Replace("/dist/server.js", "/src/server.ts");
                if (File.Exists(srcPath))
                {
                    return new DiagnosticResult
                    {
                        Name = "MCP Server Build",
                        Status = DiagnosticStatus.Fail,
                        Message = "Source found but not built",
                        Solution = "Run 'npm run build' in the unity-mcp-server directory"
                    };
                }
            }

            return new DiagnosticResult
            {
                Name = "MCP Server Build",
                Status = DiagnosticStatus.Warning,
                Message = "MCP server not found in expected locations",
                Solution = "Clone the UnityVision repository and run 'npm install && npm run build'"
            };
        }

        private DiagnosticResult CheckLocalhost()
        {
            try
            {
                var addresses = Dns.GetHostAddresses("localhost");
                if (addresses.Length > 0)
                {
                    return new DiagnosticResult
                    {
                        Name = "Localhost Resolution",
                        Status = DiagnosticStatus.Pass,
                        Message = "localhost resolves correctly"
                    };
                }
            }
            catch (Exception ex)
            {
                return new DiagnosticResult
                {
                    Name = "Localhost Resolution",
                    Status = DiagnosticStatus.Fail,
                    Message = $"Failed to resolve localhost: {ex.Message}",
                    Solution = "Check your hosts file and network configuration"
                };
            }

            return new DiagnosticResult
            {
                Name = "Localhost Resolution",
                Status = DiagnosticStatus.Warning,
                Message = "localhost resolution returned no addresses"
            };
        }

        private void CopyReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== UnityVision Diagnostics Report ===");
            report.AppendLine($"Generated: {DateTime.Now}");
            report.AppendLine($"Unity Version: {Application.unityVersion}");
            report.AppendLine($"Platform: {Application.platform}");
            report.AppendLine();

            foreach (var result in _results)
            {
                string status = result.Status switch
                {
                    DiagnosticStatus.Pass => "[PASS]",
                    DiagnosticStatus.Fail => "[FAIL]",
                    DiagnosticStatus.Warning => "[WARN]",
                    _ => "[????]"
                };
                
                report.AppendLine($"{status} {result.Name}");
                if (!string.IsNullOrEmpty(result.Message))
                    report.AppendLine($"       {result.Message}");
                if (!string.IsNullOrEmpty(result.Solution) && result.Status != DiagnosticStatus.Pass)
                    report.AppendLine($"       Solution: {result.Solution}");
                report.AppendLine();
            }

            EditorGUIUtility.systemCopyBuffer = report.ToString();
            Debug.Log("[UnityVision] Diagnostics report copied to clipboard");
        }

        private void ExportLogs()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export UnityVision Logs",
                "",
                $"unityvision_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                "txt"
            );

            if (string.IsNullOrEmpty(path)) return;

            var logs = new System.Text.StringBuilder();
            logs.AppendLine("=== UnityVision Log Export ===");
            logs.AppendLine($"Exported: {DateTime.Now}");
            logs.AppendLine($"Unity Version: {Application.unityVersion}");
            logs.AppendLine($"Project: {Application.productName}");
            logs.AppendLine();

            // Diagnostics
            logs.AppendLine("=== Diagnostics ===");
            foreach (var result in _results)
            {
                logs.AppendLine($"[{result.Status}] {result.Name}: {result.Message}");
            }
            logs.AppendLine();

            // Recent Activity
            logs.AppendLine("=== Recent Activity ===");
            var activity = BridgeConfig.RecentActivity;
            foreach (var item in activity)
            {
                string status = item.Success ? "OK" : "ERR";
                logs.AppendLine($"[{item.Timestamp:HH:mm:ss}] [{status}] {item.Method} ({item.DurationMs}ms)");
                if (!string.IsNullOrEmpty(item.Error))
                    logs.AppendLine($"    Error: {item.Error}");
            }
            logs.AppendLine();

            // Connection Info
            logs.AppendLine("=== Connection Info ===");
            logs.AppendLine($"WebSocket Connected: {Transport.WebSocketClient.IsConnected}");
            logs.AppendLine($"Port: {Transport.WebSocketClient.Port}");
            logs.AppendLine($"Request Count: {BridgeConfig.RequestCount}");
            logs.AppendLine($"Auth Required: {BridgeConfig.RequireAuth}");

            File.WriteAllText(path, logs.ToString());
            Debug.Log($"[UnityVision] Logs exported to {path}");
            EditorUtility.RevealInFinder(path);
        }

        [Serializable]
        private class ProjectEntryList
        {
            public ProjectEntry[] entries;
        }

        [Serializable]
        private class ProjectEntry
        {
            public string projectPath;
            public string projectName;
            public int port;
        }
    }

    public enum DiagnosticStatus
    {
        Pass,
        Warning,
        Fail
    }

    public class DiagnosticResult
    {
        public string Name { get; set; }
        public DiagnosticStatus Status { get; set; }
        public string Message { get; set; }
        public string Solution { get; set; }
        public Action FixAction { get; set; }
    }
}
