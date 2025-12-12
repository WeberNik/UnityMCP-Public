// ============================================================================
// UnityVision MCP Bridge - Named Pipe Server
// Provides reliable IPC between Unity and MCP server using Named Pipes
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityVision.Editor.Bridge
{
    /// <summary>
    /// Named Pipe server for Unity-MCP communication.
    /// Uses OS-level IPC instead of network stack for maximum reliability.
    /// </summary>
    public static class NamedPipeBridge
    {
        private static readonly List<Thread> _clientThreads = new List<Thread>();
        private static readonly object _lock = new object();
        private static bool _isRunning;
        private static Thread _acceptThread;
        private static CancellationTokenSource _cancellationSource;
        
        public static string PipeName { get; private set; }
        public static bool IsRunning => _isRunning;
        
        // Activity tracking
        private static List<RequestActivity> _recentActivity = new List<RequestActivity>();
        private static readonly object _activityLock = new object();
        private const int MAX_ACTIVITY_ENTRIES = 50;
        
        public static DateTime? LastRequestTime { get; private set; }
        public static int RequestCount { get; private set; }
        
        /// <summary>
        /// Initialize the Named Pipe bridge
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Generate pipe name from project path
            PipeName = GeneratePipeName();
            
            Start();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
        }
        
        /// <summary>
        /// Generate a unique pipe name based on project path
        /// </summary>
        private static string GeneratePipeName()
        {
            string projectPath = Application.dataPath;
            if (projectPath.EndsWith("/Assets"))
                projectPath = projectPath.Substring(0, projectPath.Length - 7);
            
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(projectPath));
                string hashStr = BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLower();
                return $"unityvision-{hashStr}";
            }
        }
        
        /// <summary>
        /// Start the Named Pipe server
        /// </summary>
        public static void Start()
        {
            if (_isRunning) return;
            
            try
            {
                _cancellationSource = new CancellationTokenSource();
                _isRunning = true;
                
                // Start accept thread
                _acceptThread = new Thread(AcceptLoop)
                {
                    IsBackground = true,
                    Name = "UnityVision-PipeAccept"
                };
                _acceptThread.Start();
                
                Debug.Log($"[UnityVision] Named Pipe server started: \\\\.\\pipe\\{PipeName}");
                
                // Register with project registry
                Registry.ProjectRegistry.RegisterProject();
            }
            catch (Exception ex)
            {
                _isRunning = false;
                Debug.LogError($"[UnityVision] Failed to start Named Pipe server: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Stop the Named Pipe server
        /// </summary>
        public static void Stop()
        {
            if (!_isRunning) return;
            
            _isRunning = false;
            _cancellationSource?.Cancel();
            
            // Clean up client threads
            lock (_lock)
            {
                foreach (var thread in _clientThreads)
                {
                    try { thread.Interrupt(); } catch { }
                }
                _clientThreads.Clear();
            }
            
            Debug.Log("[UnityVision] Named Pipe server stopped");
        }
        
        /// <summary>
        /// Accept loop - waits for new client connections
        /// </summary>
        private static void AcceptLoop()
        {
            while (_isRunning && !_cancellationSource.Token.IsCancellationRequested)
            {
                NamedPipeServerStream pipeServer = null;
                try
                {
                    // Create a new pipe server instance for each client
                    pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous
                    );
                    
                    // Wait for a client to connect
                    pipeServer.WaitForConnection();
                    
                    if (!_isRunning) 
                    {
                        pipeServer.Close();
                        break;
                    }
                    
                    // Handle client in a new thread
                    var clientPipe = pipeServer;
                    var clientThread = new Thread(() => HandleClient(clientPipe))
                    {
                        IsBackground = true,
                        Name = "UnityVision-PipeClient"
                    };
                    
                    lock (_lock)
                    {
                        _clientThreads.Add(clientThread);
                    }
                    
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Debug.LogWarning($"[UnityVision] Pipe accept error: {ex.Message}");
                    }
                    pipeServer?.Close();
                }
            }
        }
        
        /// <summary>
        /// Handle a connected client
        /// </summary>
        private static void HandleClient(NamedPipeServerStream pipe)
        {
            try
            {
                using (pipe)
                using (var reader = new StreamReader(pipe, Encoding.UTF8))
                using (var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true })
                {
                    while (pipe.IsConnected && _isRunning)
                    {
                        // Read one line (newline-delimited JSON)
                        string line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line)) break;
                        
                        // Process the request
                        string response = ProcessRequest(line);
                        
                        // Write response (newline-delimited)
                        writer.WriteLine(response);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Debug.LogWarning($"[UnityVision] Client handler error: {ex.Message}");
                }
            }
            finally
            {
                // Remove from client list
                lock (_lock)
                {
                    _clientThreads.RemoveAll(t => t == Thread.CurrentThread);
                }
            }
        }
        
        /// <summary>
        /// Process a JSON-RPC request and return the response
        /// </summary>
        private static string ProcessRequest(string requestJson)
        {
            var startTime = DateTime.Now;
            string methodName = "unknown";
            string requestId = $"unity-{RequestCount + 1}";
            
            FileLogger.Log("DEBUG", "PipeBridge", $"REQUEST_RECEIVED {requestId}", requestJson.Length > 500 ? requestJson.Substring(0, 500) : requestJson);
            
            try
            {
                // Parse request
                FileLogger.LogRequestPhase(requestId, "PARSING_JSON");
                var request = JsonUtility.FromJson<RpcRequest>(requestJson);
                if (request == null)
                {
                    FileLogger.LogRequestEnd(requestId, "unknown", (int)(DateTime.Now - startTime).TotalMilliseconds, false, "PARSE_ERROR");
                    return JsonUtility.ToJson(RpcResponse.Failure("PARSE_ERROR", "Failed to parse request"));
                }
                
                methodName = request.Method ?? "unknown";
                requestId = $"unity-{++RequestCount}-{methodName}";
                LastRequestTime = DateTime.Now;
                
                FileLogger.LogRequestStart(requestId, methodName);
                FileLogger.LogRequestPhase(requestId, "PARSED", $"Method: {methodName}");
                
                // Execute on main thread
                RpcResponse response = null;
                var waitHandle = new ManualResetEvent(false);
                bool delayCallScheduled = false;
                
                FileLogger.LogRequestPhase(requestId, "SCHEDULING_MAIN_THREAD");
                
                EditorApplication.delayCall += () =>
                {
                    FileLogger.LogRequestPhase(requestId, "MAIN_THREAD_EXECUTING", $"Elapsed: {(DateTime.Now - startTime).TotalMilliseconds}ms");
                    try
                    {
                        response = RpcHandler.HandleRequest(request);
                        FileLogger.LogRequestPhase(requestId, "HANDLER_COMPLETED", $"HasError: {response?.Error != null}");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogError("PipeBridge", $"Handler exception for {requestId}: {ex.Message}", ex);
                        response = RpcResponse.Failure("INTERNAL_ERROR", ex.Message);
                    }
                    finally
                    {
                        FileLogger.LogRequestPhase(requestId, "SIGNALING_WAIT_HANDLE");
                        waitHandle.Set();
                    }
                };
                delayCallScheduled = true;
                
                FileLogger.LogRequestPhase(requestId, "DELAY_CALL_SCHEDULED", $"Waiting for main thread...");
                
                // Wait for main thread execution (with timeout)
                // Log every 5 seconds while waiting
                int waitedMs = 0;
                const int checkIntervalMs = 5000;
                const int maxWaitMs = 30000;
                
                while (waitedMs < maxWaitMs)
                {
                    if (waitHandle.WaitOne(checkIntervalMs))
                    {
                        FileLogger.LogRequestPhase(requestId, "WAIT_HANDLE_SIGNALED", $"After {waitedMs + (DateTime.Now - startTime).TotalMilliseconds - waitedMs}ms");
                        break;
                    }
                    waitedMs += checkIntervalMs;
                    FileLogger.Log("WARN", "PipeBridge", $"STILL_WAITING {requestId} - {waitedMs}ms elapsed", 
                        $"IsCompiling: {EditorApplication.isCompiling}, IsPlaying: {EditorApplication.isPlaying}");
                }
                
                if (waitedMs >= maxWaitMs && response == null)
                {
                    FileLogger.LogRequestEnd(requestId, methodName, maxWaitMs, false, "TIMEOUT - Main thread never executed delayCall");
                    RecordActivity(methodName, maxWaitMs, false, "Timeout");
                    return JsonUtility.ToJson(RpcResponse.Failure("TIMEOUT", 
                        $"Request timed out waiting for Unity main thread after {maxWaitMs}ms. " +
                        $"IsCompiling: {EditorApplication.isCompiling}, IsPlaying: {EditorApplication.isPlaying}"));
                }
                
                // Record activity
                int durationMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                bool success = response?.Error == null;
                string error = success ? null : response?.Error?.Message;
                RecordActivity(methodName, durationMs, success, error);
                
                FileLogger.LogRequestEnd(requestId, methodName, durationMs, success, error);
                
                string responseJson = JsonUtility.ToJson(response);
                FileLogger.Log("DEBUG", "PipeBridge", $"RESPONSE_SENDING {requestId}", $"Length: {responseJson.Length}");
                
                return responseJson;
            }
            catch (Exception ex)
            {
                int durationMs = (int)(DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.LogError("PipeBridge", $"ProcessRequest exception for {requestId}", ex);
                FileLogger.LogRequestEnd(requestId, methodName, durationMs, false, ex.Message);
                RecordActivity(methodName, durationMs, false, ex.Message);
                return JsonUtility.ToJson(RpcResponse.Failure("INTERNAL_ERROR", ex.Message));
            }
        }
        
        /// <summary>
        /// Record activity for monitoring
        /// </summary>
        private static void RecordActivity(string method, int durationMs, bool success, string error)
        {
            lock (_activityLock)
            {
                _recentActivity.Insert(0, new RequestActivity
                {
                    Method = method,
                    Timestamp = DateTime.Now,
                    DurationMs = durationMs,
                    Success = success,
                    Error = error
                });
                
                // Trim to max entries
                while (_recentActivity.Count > MAX_ACTIVITY_ENTRIES)
                {
                    _recentActivity.RemoveAt(_recentActivity.Count - 1);
                }
            }
        }
        
        /// <summary>
        /// Get recent activity for UI display
        /// </summary>
        public static List<RequestActivity> GetRecentActivity()
        {
            lock (_activityLock)
            {
                return new List<RequestActivity>(_recentActivity);
            }
        }
        
        /// <summary>
        /// Get status info for health checks
        /// </summary>
        public static object GetStatus()
        {
            string projectPath = Application.dataPath;
            if (projectPath.EndsWith("/Assets"))
                projectPath = projectPath.Substring(0, projectPath.Length - 7);
            
            return new
            {
                status = "ok",
                pipeName = PipeName,
                projectName = Path.GetFileName(projectPath),
                projectPath = projectPath,
                unityVersion = Application.unityVersion,
                isCompiling = EditorApplication.isCompiling,
                requestCount = RequestCount,
                lastRequestTime = LastRequestTime?.ToString("o")
            };
        }
    }
}
