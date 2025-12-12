// ============================================================================
// UnityVision Bridge - Main Thread Dispatcher
// Utility for dispatching actions to Unity's main thread
// ============================================================================

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace UnityVision.Editor.Utils
{
    /// <summary>
    /// Dispatcher for executing actions on Unity's main thread.
    /// Essential for MCP commands that need to interact with Unity API.
    /// </summary>
    [InitializeOnLoad]
    public static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _pendingActions = new ConcurrentQueue<Action>();
        private static readonly ConcurrentQueue<Func<IEnumerator>> _pendingCoroutines = new ConcurrentQueue<Func<IEnumerator>>();
        
        static MainThreadDispatcher()
        {
            EditorApplication.update += ProcessQueue;
        }
        
        /// <summary>
        /// Queue an action to be executed on the main thread
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            _pendingActions.Enqueue(action);
        }
        
        /// <summary>
        /// Queue a coroutine to be executed on the main thread
        /// </summary>
        public static void EnqueueCoroutine(Func<IEnumerator> coroutineFactory)
        {
            if (coroutineFactory == null) return;
            _pendingCoroutines.Enqueue(coroutineFactory);
        }
        
        /// <summary>
        /// Execute an action on the main thread and wait for completion
        /// </summary>
        public static Task ExecuteAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Execute a function on the main thread and return the result
        /// </summary>
        public static Task<T> ExecuteAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>();
            
            Enqueue(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Execute a coroutine on the main thread and wait for completion
        /// </summary>
        public static Task ExecuteCoroutineAsync(Func<TaskCompletionSource<bool>, IEnumerator> coroutineFactory)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            EnqueueCoroutine(() => coroutineFactory(tcs));
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Process queued actions and coroutines
        /// </summary>
        private static void ProcessQueue()
        {
            // Process actions
            while (_pendingActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] Error executing action: {ex.Message}");
                }
            }
            
            // Process coroutines
            while (_pendingCoroutines.TryDequeue(out var coroutineFactory))
            {
                try
                {
                    var coroutine = coroutineFactory();
                    EditorCoroutineRunner.StartCoroutine(coroutine);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] Error starting coroutine: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Simple editor coroutine runner (doesn't require Unity.EditorCoroutines package)
    /// </summary>
    public static class EditorCoroutineRunner
    {
        private static readonly System.Collections.Generic.List<IEnumerator> _coroutines = 
            new System.Collections.Generic.List<IEnumerator>();
        
        static EditorCoroutineRunner()
        {
            EditorApplication.update += Update;
        }
        
        /// <summary>
        /// Start a coroutine in the editor
        /// </summary>
        public static void StartCoroutine(IEnumerator coroutine)
        {
            if (coroutine != null)
            {
                _coroutines.Add(coroutine);
            }
        }
        
        /// <summary>
        /// Stop all coroutines
        /// </summary>
        public static void StopAllCoroutines()
        {
            _coroutines.Clear();
        }
        
        private static void Update()
        {
            for (int i = _coroutines.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!_coroutines[i].MoveNext())
                    {
                        _coroutines.RemoveAt(i);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorCoroutineRunner] Coroutine error: {ex.Message}");
                    _coroutines.RemoveAt(i);
                }
            }
        }
    }
}
