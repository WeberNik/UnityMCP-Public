// ============================================================================
// UnityVision Bridge - WebSocket Client
// Connects to the MCP server's WebSocket hub for bidirectional communication
// ============================================================================

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityVision.Editor.Bridge;
using UnityVision.Editor.Tools;
using UnityEditorInternal;

namespace UnityVision.Editor.Transport
{
    /// <summary>
    /// WebSocket client that connects to the MCP server for command execution.
    /// - Exponential backoff reconnection
    /// - SemaphoreSlim for thread-safe sending
    /// - Play mode and assembly reload handling
    /// </summary>
    [InitializeOnLoad]
    public static class WebSocketClient
    {
        private static ClientWebSocket _socket;
        private static CancellationTokenSource _cts;
        private static Thread _receiveThread;
        private static bool _isConnected;
        private static bool _isConnecting;
        private static string _sessionId;
        private static DateTime _lastPingTime;
        private static DateTime _connectedSince;
        private static int _reconnectAttempts;
        private static volatile int _isReconnectingFlag;
        
        // Background keepalive to force Unity updates when unfocused
        private static Thread _keepaliveThread;
        private static volatile bool _hasPendingCommands;
        private static DateTime _lastMainThreadActivity;
        
        // Thread-safe send lock
        private static readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        
        // Exponential backoff schedule
        private static readonly TimeSpan[] ReconnectSchedule = new[]
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        };
        
        // Configuration
        private const int DEFAULT_PORT = 7890;
        private const int PING_INTERVAL_MS = 15000;
        
        // Pending command responses
        private static readonly Dictionary<string, TaskCompletionSource<JObject>> _pendingCommands = 
            new Dictionary<string, TaskCompletionSource<JObject>>();
        private static readonly object _lock = new object();
        
        // Events
        public static event Action OnConnected;
        public static event Action OnDisconnected;
        public static event Action<string> OnError;
        
        // Properties
        public static bool IsConnected => _isConnected;
        public static string SessionId => _sessionId;
        public static DateTime? ConnectedSince => _isConnected ? _connectedSince : (DateTime?)null;
        public static int Port { get; private set; } = DEFAULT_PORT;
        
        static WebSocketClient()
        {
            // Read port from environment or use default
            var portEnv = Environment.GetEnvironmentVariable("UNITY_VISION_WS_PORT");
            if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int port))
            {
                Port = port;
            }
            
            // Subscribe to Unity lifecycle events
            EditorApplication.delayCall += () => ConnectAsync();
            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += Update;
            
            // Start background keepalive thread to force Unity updates when unfocused
            StartKeepaliveThread();
            _lastMainThreadActivity = DateTime.Now;
        }
        
        /// <summary>
        /// Start the background keepalive thread that forces Unity to process messages when unfocused
        /// </summary>
        private static void StartKeepaliveThread()
        {
            if (_keepaliveThread != null && _keepaliveThread.IsAlive)
                return;
                
            _keepaliveThread = new Thread(KeepaliveLoop)
            {
                IsBackground = true,
                Name = "UnityVision Keepalive"
            };
            _keepaliveThread.Start();
        }
        
        /// <summary>
        /// Background thread that forces Unity to update when there are pending commands
        /// or when the main thread has been stale for too long
        /// </summary>
        private static void KeepaliveLoop()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(500); // Check every 500ms
                    
                    if (!_isConnected)
                        continue;
                    
                    // Force update if we have pending commands OR main thread is stale
                    var timeSinceMainThread = (DateTime.Now - _lastMainThreadActivity).TotalSeconds;
                    
                    if (_hasPendingCommands || timeSinceMainThread > 2.0)
                    {
                        ForceEditorUpdate();
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                catch
                {
                    // Ignore errors in keepalive
                }
            }
        }
        
        /// <summary>
        /// Force Unity Editor to process pending callbacks even when unfocused
        /// </summary>
        private static void ForceEditorUpdate()
        {
            try
            {
                // This forces Unity to repaint and process pending EditorApplication.delayCall callbacks
                InternalEditorUtility.RepaintAllViews();
            }
            catch
            {
                // Ignore - may fail during shutdown
            }
        }
        
        /// <summary>
        /// Handle editor quitting
        /// </summary>
        private static void OnEditorQuitting()
        {
            FileLogger.Log("INFO", "WebSocketClient", "Editor quitting, disconnecting...");
            Disconnect();
        }
        
        /// <summary>
        /// Handle before assembly reload
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            if (_isConnected)
            {
                FileLogger.Log("INFO", "WebSocketClient", "Assembly reload, disconnecting...");
                Disconnect();
            }
        }
        
        /// <summary>
        /// Handle after assembly reload - reconnect if was connected
        /// </summary>
        private static void OnAfterAssemblyReload()
        {
            // Auto-reconnect after assembly reload
            EditorApplication.delayCall += () => ConnectAsync();
        }
        
        /// <summary>
        /// Handle play mode state changes
        /// </summary>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // About to enter Play Mode - disconnect
                    if (_isConnected)
                    {
                        FileLogger.Log("INFO", "WebSocketClient", "Entering play mode, disconnecting...");
                        Disconnect();
                    }
                    break;
                    
                case PlayModeStateChange.EnteredEditMode:
                    // Returned to Edit Mode - reconnect
                    EditorApplication.delayCall += () => ConnectAsync();
                    break;
            }
        }
        
        private static void Update()
        {
            // Track main thread activity for keepalive
            _lastMainThreadActivity = DateTime.Now;
            
            // Periodic ping to keep connection alive
            if (_isConnected && (DateTime.Now - _lastPingTime).TotalMilliseconds > PING_INTERVAL_MS)
            {
                _ = SendPingAsync();
                _lastPingTime = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Connect to the MCP server's WebSocket hub
        /// </summary>
        public static async void ConnectAsync()
        {
            if (_isConnected || _isConnecting) return;
            
            _isConnecting = true;
            _cts = new CancellationTokenSource();
            
            try
            {
                _socket = new ClientWebSocket();
                var uri = new Uri($"ws://localhost:{Port}");
                
                FileLogger.Log("INFO", "WebSocketClient", $"Connecting to {uri}...");
                
                await _socket.ConnectAsync(uri, _cts.Token);
                
                _isConnected = true;
                _isConnecting = false;
                _connectedSince = DateTime.Now;
                _lastPingTime = DateTime.Now;
                _reconnectAttempts = 0;
                
                FileLogger.Log("INFO", "WebSocketClient", "Connected to MCP server");
                Debug.Log("[UnityVision] Connected to MCP server");
                
                // Start receive loop
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();
                
                // Send registration message
                await SendRegistrationAsync();
                
                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                _isConnecting = false;
                FileLogger.Log("WARN", "WebSocketClient", $"Connection failed: {ex.Message}");
                
                // Schedule reconnect with exponential backoff
                if (_reconnectAttempts < ReconnectSchedule.Length)
                {
                    var delay = ReconnectSchedule[_reconnectAttempts];
                    _reconnectAttempts++;
                    
                    if (delay > TimeSpan.Zero)
                    {
                        FileLogger.Log("INFO", "WebSocketClient", $"Reconnecting in {delay.TotalSeconds}s (attempt {_reconnectAttempts})...");
                    }
                    
                    // Use Task.Delay instead of Thread.Sleep to not block
                    Task.Run(async () =>
                    {
                        await Task.Delay(delay);
                        EditorApplication.delayCall += () => ConnectAsync();
                    });
                }
                else
                {
                    Debug.LogWarning($"[UnityVision] Could not connect to MCP server after {ReconnectSchedule.Length} attempts. Is Windsurf running with UnityVision MCP enabled?");
                }
            }
        }
        
        /// <summary>
        /// Disconnect from the MCP server
        /// </summary>
        public static void Disconnect()
        {
            if (!_isConnected && !_isConnecting) return;
            
            _isConnected = false;
            _isConnecting = false;
            _sessionId = null;
            
            try
            {
                _cts?.Cancel();
                
                if (_socket?.State == WebSocketState.Open)
                {
                    _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unity closing", CancellationToken.None).Wait(1000);
                }
                
                _socket?.Dispose();
                _socket = null;
            }
            catch { /* ignore cleanup errors */ }
            
            // Reject all pending commands
            lock (_lock)
            {
                foreach (var pending in _pendingCommands.Values)
                {
                    pending.TrySetException(new Exception("WebSocket disconnected"));
                }
                _pendingCommands.Clear();
            }
            
            FileLogger.Log("INFO", "WebSocketClient", "Disconnected from MCP server");
            OnDisconnected?.Invoke();
        }
        
        /// <summary>
        /// Reconnect to the MCP server
        /// </summary>
        public static void Reconnect()
        {
            Disconnect();
            _reconnectAttempts = 0;
            ConnectAsync();
        }
        
        /// <summary>
        /// Send registration message to identify this Unity instance
        /// </summary>
        private static async Task SendRegistrationAsync()
        {
            var projectPath = Application.dataPath.Replace("/Assets", "");
            var projectName = System.IO.Path.GetFileName(projectPath);
            var projectHash = ComputeHash(projectPath);
            
            var message = new JObject
            {
                ["type"] = "register",
                ["project_name"] = projectName,
                ["project_hash"] = projectHash,
                ["unity_version"] = Application.unityVersion,
                ["client_name"] = "UnityVision Bridge", // Client name tracking (Phase 48)
                ["platform"] = Application.platform.ToString()
            };
            
            await SendMessageAsync(message);
        }
        
        /// <summary>
        /// Send tool registration to MCP server (Phase 45)
        /// </summary>
        private static async Task SendToolRegistrationAsync()
        {
            // Initialize tool registry if needed
            Tools.ToolRegistry.Initialize();
            
            var tools = Tools.ToolRegistry.GetToolDefinitionsJson();
            
            if (tools.Count == 0)
            {
                FileLogger.Log("INFO", "WebSocketClient", "No custom tools to register");
                return;
            }
            
            var message = new JObject
            {
                ["type"] = "register_tools",
                ["tools"] = tools
            };
            
            await SendMessageAsync(message);
            FileLogger.Log("INFO", "WebSocketClient", $"Sent {tools.Count} custom tools registration");
        }
        
        /// <summary>
        /// Send a ping to keep the connection alive
        /// </summary>
        private static async Task SendPingAsync()
        {
            if (!_isConnected) return;
            
            var message = new JObject
            {
                ["type"] = "ping",
                ["session_id"] = _sessionId
            };
            
            try
            {
                await SendMessageAsync(message);
            }
            catch { /* ignore ping failures */ }
        }
        
        /// <summary>
        /// Send a message to the MCP server (thread-safe with SemaphoreSlim)
        /// </summary>
        private static async Task SendMessageAsync(JObject message)
        {
            if (_socket?.State != WebSocketState.Open) return;
            
            var json = message.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            // Use SemaphoreSlim for thread-safe sending
            await _sendLock.WaitAsync(_cts.Token);
            try
            {
                if (_socket?.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException("WebSocket is not open");
                }
                
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );
            }
            finally
            {
                _sendLock.Release();
            }
        }
        
        /// <summary>
        /// Receive loop for incoming messages
        /// </summary>
        private static async void ReceiveLoop()
        {
            var buffer = new byte[8192];
            var messageBuilder = new StringBuilder();
            
            try
            {
                while (_isConnected && _socket?.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        _cts.Token
                    );
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    
                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    
                    if (result.EndOfMessage)
                    {
                        var json = messageBuilder.ToString();
                        messageBuilder.Clear();
                        
                        // Signal that we have pending commands - triggers keepalive to force update
                        _hasPendingCommands = true;
                        
                        // Process on main thread
                        var message = json;
                        EditorApplication.delayCall += () => ProcessMessage(message);
                    }
                }
            }
            catch (OperationCanceledException) { /* expected on disconnect */ }
            catch (Exception ex)
            {
                FileLogger.Log("ERROR", "WebSocketClient", $"Receive error: {ex.Message}");
            }
            
            // Connection lost - trigger reconnect
            if (_isConnected)
            {
                _isConnected = false;
                OnDisconnected?.Invoke();
                
                EditorApplication.delayCall += () =>
                {
                    Debug.LogWarning("[UnityVision] Connection to MCP server lost. Reconnecting...");
                    ConnectAsync();
                };
            }
        }
        
        /// <summary>
        /// Process an incoming message from the MCP server
        /// </summary>
        private static void ProcessMessage(string json)
        {
            // Clear pending flag - we're processing now
            _hasPendingCommands = false;
            
            try
            {
                var message = JObject.Parse(json);
                var type = message["type"]?.ToString();
                
                switch (type)
                {
                    case "welcome":
                        FileLogger.Log("INFO", "WebSocketClient", "Received welcome from MCP server");
                        break;
                        
                    case "registered":
                        _sessionId = message["session_id"]?.ToString();
                        FileLogger.Log("INFO", "WebSocketClient", $"Registered with session: {_sessionId}");
                        Debug.Log($"[UnityVision] Registered with MCP server (session: {_sessionId?.Substring(0, 8)}...)");
                        // Send custom tool registration (Phase 45)
                        _ = SendToolRegistrationAsync();
                        break;
                        
                    case "execute":
                        HandleExecuteCommand(message);
                        break;
                        
                    case "pong":
                        // Ping response received
                        break;
                        
                    default:
                        FileLogger.Log("WARN", "WebSocketClient", $"Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log("ERROR", "WebSocketClient", $"Error processing message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handle an execute command from the MCP server
        /// </summary>
        private static async void HandleExecuteCommand(JObject message)
        {
            var commandId = message["id"]?.ToString();
            var commandName = message["name"]?.ToString();
            var parameters = message["params"] as JObject ?? new JObject();
            
            FileLogger.Log("INFO", "WebSocketClient", $"Executing command: {commandName} ({commandId})");
            
            JObject resultMessage = new JObject
            {
                ["type"] = "command_result",
                ["id"] = commandId
            };
            
            bool success = false;
            string errorMsg = null;
            
            try
            {
                // First, check if this is a custom tool (Phase 45)
                if (ToolRegistry.TryGetTool(commandName, out var customTool))
                {
                    FileLogger.Log("INFO", "WebSocketClient", $"Executing custom tool: {commandName}");
                    
                    JObject toolResult;
                    if (customTool.IsAsync)
                    {
                        var tcs = new TaskCompletionSource<JObject>();
                        customTool.ExecuteAsync(parameters, tcs);
                        toolResult = await tcs.Task;
                    }
                    else
                    {
                        toolResult = customTool.Execute(parameters);
                    }
                    
                    // Check if tool returned an error
                    if (toolResult.ContainsKey("error"))
                    {
                        resultMessage["error"] = toolResult["error"];
                        errorMsg = toolResult["error"]?["message"]?.ToString();
                    }
                    else
                    {
                        resultMessage["result"] = toolResult;
                        success = true;
                    }
                }
                else
                {
                    // Fall back to RpcHandler for built-in commands
                    var request = new RpcRequest
                    {
                        Method = commandName,
                        Params = parameters
                    };
                    
                    var response = RpcHandler.HandleRequest(request);
                    
                    if (response.Error != null)
                    {
                        resultMessage["error"] = new JObject
                        {
                            ["code"] = response.Error.Code,
                            ["message"] = response.Error.Message
                        };
                        errorMsg = response.Error.Message;
                    }
                    else
                    {
                        resultMessage["result"] = JToken.FromObject(response.Result ?? new { success = true });
                        success = true;
                    }
                }
                
                await SendMessageAsync(resultMessage);
                FileLogger.Log("INFO", "WebSocketClient", $"Command completed: {commandName} (success: {success})");
            }
            catch (Exception ex)
            {
                FileLogger.Log("ERROR", "WebSocketClient", $"Command failed: {commandName} - {ex.Message}");
                errorMsg = ex.Message;
                
                // Send error back
                resultMessage["error"] = new JObject
                {
                    ["code"] = "EXECUTION_ERROR",
                    ["message"] = ex.Message
                };
                
                await SendMessageAsync(resultMessage);
            }
            
            // Record activity for UI
            BridgeConfig.RecordActivity(commandName, 0, success, errorMsg);
        }
        
        /// <summary>
        /// Compute a hash for the project path
        /// </summary>
        private static string ComputeHash(string input)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            }
        }
    }
}
