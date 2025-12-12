// ============================================================================
// UnityVision Bridge - Command Dispatcher
// Queue-based command execution on Unity's main thread
// Uses EditorApplication.update for reliable, non-blocking execution
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Transport
{
    /// <summary>
    /// Centralized command execution pipeline.
    /// Guarantees commands are executed on Unity's main thread without blocking.
    /// </summary>
    public static class CommandDispatcher
    {
        // ============================================================================
        // Pending Command
        // ============================================================================
        
        private sealed class PendingCommand
        {
            public string Id { get; }
            public string CommandName { get; }
            public JObject Parameters { get; }
            public TaskCompletionSource<string> CompletionSource { get; }
            public CancellationToken CancellationToken { get; }
            public CancellationTokenRegistration CancellationRegistration { get; }
            public DateTime QueuedAt { get; }
            public bool IsExecuting { get; set; }
            
            public PendingCommand(
                string id,
                string commandName,
                JObject parameters,
                TaskCompletionSource<string> completionSource,
                CancellationToken cancellationToken,
                CancellationTokenRegistration registration)
            {
                Id = id;
                CommandName = commandName;
                Parameters = parameters;
                CompletionSource = completionSource;
                CancellationToken = cancellationToken;
                CancellationRegistration = registration;
                QueuedAt = DateTime.Now;
                IsExecuting = false;
            }
            
            public void Dispose()
            {
                CancellationRegistration.Dispose();
            }
            
            public void TrySetResult(string payload)
            {
                CompletionSource.TrySetResult(payload);
            }
            
            public void TrySetCanceled()
            {
                CompletionSource.TrySetCanceled(CancellationToken);
            }
            
            public void TrySetException(Exception ex)
            {
                CompletionSource.TrySetException(ex);
            }
        }
        
        // ============================================================================
        // State
        // ============================================================================
        
        private static readonly Dictionary<string, PendingCommand> _pending = new Dictionary<string, PendingCommand>();
        private static readonly object _pendingLock = new object();
        private static bool _updateHooked;
        private static bool _initialized;
        private static int _commandCounter;
        
        // ============================================================================
        // Public API
        // ============================================================================
        
        /// <summary>
        /// Execute a command on the Unity main thread and await its JSON response.
        /// This method is non-blocking and safe to call from any thread.
        /// </summary>
        public static Task<string> ExecuteCommandAsync(
            string commandName,
            JObject parameters,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(commandName))
            {
                throw new ArgumentNullException(nameof(commandName));
            }
            
            EnsureInitialized();
            
            var id = $"cmd-{Interlocked.Increment(ref _commandCounter)}";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            var registration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() => CancelPending(id, cancellationToken))
                : default;
            
            var pending = new PendingCommand(id, commandName, parameters ?? new JObject(), tcs, cancellationToken, registration);
            
            lock (_pendingLock)
            {
                _pending[id] = pending;
                HookUpdate();
            }
            
            FileLogger.Log("DEBUG", "CommandDispatcher", $"Queued command {commandName} ({id})");
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Get the number of pending commands in the queue.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                lock (_pendingLock)
                {
                    return _pending.Count;
                }
            }
        }
        
        // ============================================================================
        // Initialization
        // ============================================================================
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            // Initialize the RPC handler if needed
            _initialized = true;
            FileLogger.Log("INFO", "CommandDispatcher", "Initialized");
        }
        
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            // Clean up on domain reload
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }
        
        private static void OnBeforeAssemblyReload()
        {
            // Cancel all pending commands
            lock (_pendingLock)
            {
                foreach (var kvp in _pending)
                {
                    kvp.Value.TrySetCanceled();
                    kvp.Value.Dispose();
                }
                _pending.Clear();
                UnhookUpdate();
            }
        }
        
        // ============================================================================
        // Update Hook
        // ============================================================================
        
        private static void HookUpdate()
        {
            if (_updateHooked) return;
            
            _updateHooked = true;
            EditorApplication.update += ProcessQueue;
            FileLogger.Log("DEBUG", "CommandDispatcher", "Hooked to EditorApplication.update");
        }
        
        private static void UnhookUpdate()
        {
            if (!_updateHooked) return;
            
            _updateHooked = false;
            EditorApplication.update -= ProcessQueue;
            FileLogger.Log("DEBUG", "CommandDispatcher", "Unhooked from EditorApplication.update");
        }
        
        private static void UnhookUpdateIfIdle()
        {
            if (_pending.Count > 0 || !_updateHooked) return;
            UnhookUpdate();
        }
        
        // ============================================================================
        // Queue Processing (runs on main thread every frame)
        // ============================================================================
        
        private static void ProcessQueue()
        {
            List<(string id, PendingCommand pending)> ready;
            
            lock (_pendingLock)
            {
                ready = new List<(string, PendingCommand)>(_pending.Count);
                
                foreach (var kvp in _pending)
                {
                    if (kvp.Value.IsExecuting) continue;
                    
                    kvp.Value.IsExecuting = true;
                    ready.Add((kvp.Key, kvp.Value));
                }
                
                if (ready.Count == 0)
                {
                    UnhookUpdateIfIdle();
                    return;
                }
            }
            
            // Process commands on main thread
            foreach (var (id, pending) in ready)
            {
                ProcessCommand(id, pending);
            }
        }
        
        private static void ProcessCommand(string id, PendingCommand pending)
        {
            var startTime = DateTime.Now;
            
            // Check for cancellation
            if (pending.CancellationToken.IsCancellationRequested)
            {
                RemovePending(id, pending);
                pending.TrySetCanceled();
                return;
            }
            
            FileLogger.Log("DEBUG", "CommandDispatcher", $"Executing {pending.CommandName} ({id})");
            
            try
            {
                // Build the RPC request
                var request = new RpcRequest
                {
                    Method = pending.CommandName,
                    Params = pending.Parameters
                };
                
                // Execute via RpcHandler
                var response = RpcHandler.HandleRequest(request);
                
                var duration = (int)(DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.Log("INFO", "CommandDispatcher", $"Completed {pending.CommandName} ({id}) in {duration}ms");
                
                // Convert response to JSON
                string responseJson;
                
                // If the response has a result, extract it
                if (response.Result != null)
                {
                    // Serialize the result object to JSON string first
                    string resultJson = JsonConvert.SerializeObject(response.Result);
                    var resultObj = new JObject
                    {
                        ["status"] = "success",
                        ["result"] = JToken.Parse(resultJson)
                    };
                    responseJson = resultObj.ToString(Formatting.None);
                }
                else if (response.Error != null)
                {
                    var errorObj = new JObject
                    {
                        ["status"] = "error",
                        ["error"] = response.Error.Message,
                        ["code"] = response.Error.Code
                    };
                    responseJson = errorObj.ToString(Formatting.None);
                }
                else
                {
                    responseJson = "{\"status\":\"success\",\"result\":null}";
                }
                
                pending.TrySetResult(responseJson);
            }
            catch (Exception ex)
            {
                var duration = (int)(DateTime.Now - startTime).TotalMilliseconds;
                FileLogger.LogError("CommandDispatcher", $"Error executing {pending.CommandName}: {ex.Message}", ex);
                
                var errorResponse = new JObject
                {
                    ["status"] = "error",
                    ["error"] = ex.Message,
                    ["stackTrace"] = ex.StackTrace
                };
                
                pending.TrySetResult(errorResponse.ToString(Formatting.None));
            }
            finally
            {
                RemovePending(id, pending);
            }
        }
        
        // ============================================================================
        // Helpers
        // ============================================================================
        
        private static void CancelPending(string id, CancellationToken token)
        {
            PendingCommand pending = null;
            
            lock (_pendingLock)
            {
                if (_pending.TryGetValue(id, out pending))
                {
                    _pending.Remove(id);
                    UnhookUpdateIfIdle();
                }
            }
            
            if (pending != null)
            {
                pending.TrySetCanceled();
                pending.Dispose();
                FileLogger.Log("DEBUG", "CommandDispatcher", $"Cancelled command {id}");
            }
        }
        
        private static void RemovePending(string id, PendingCommand pending)
        {
            lock (_pendingLock)
            {
                _pending.Remove(id);
                UnhookUpdateIfIdle();
            }
            
            pending.Dispose();
        }
    }
}
