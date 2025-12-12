// ============================================================================
// UnityVision Bridge - Editor Handlers
// Handlers for editor state and play mode control
// Enhanced with async recompilation tracking
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class EditorHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetEditorStateResponse
        {
            public string unityVersion;
            public string projectPath;
            public bool isPlaying;
            public bool isPaused;
            public string activeScene;
            public List<string> loadedScenes;
            public string platform;
        }

        [Serializable]
        public class SetPlayModeRequest
        {
            public string mode; // "play", "pause", "stop"
        }

        [Serializable]
        public class SetPlayModeResponse
        {
            public bool success;
            public string previousMode;
            public string currentMode;
        }

        [Serializable]
        public class GetActiveContextRequest
        {
            public int maxConsoleErrors = 5;
            public bool includeSelection = true;
            public bool includePlayModeState = true;
        }

        [Serializable]
        public class SelectedObjectInfo
        {
            public string name;
            public string path;
            public string type;
            public List<string> components;
        }

        [Serializable]
        public class ConsoleErrorInfo
        {
            public long timestamp;
            public string type;
            public string message;
        }

        [Serializable]
        public class GetActiveContextResponse
        {
            public string playModeState;
            public bool isCompiling;
            public string activeScene;
            public SelectedObjectInfo selectedObject;
            public List<SelectedObjectInfo> selectedObjects;
            public List<ConsoleErrorInfo> recentErrors;
        }

        #endregion

        public static RpcResponse GetEditorState(RpcRequest request)
        {
            var loadedScenes = new List<string>();
            string activeScene = "";

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    loadedScenes.Add(scene.name);
                }
            }

            var active = SceneManager.GetActiveScene();
            if (active.IsValid())
            {
                activeScene = active.name;
            }

            var response = new GetEditorStateResponse
            {
                unityVersion = Application.unityVersion,
                projectPath = Application.dataPath.Replace("/Assets", ""),
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                activeScene = activeScene,
                loadedScenes = loadedScenes,
                platform = Application.platform.ToString()
            };

            return RpcResponse.Success(response);
        }

        public static RpcResponse SetPlayMode(RpcRequest request)
        {
            var req = request.GetParams<SetPlayModeRequest>();

            if (string.IsNullOrEmpty(req.mode))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "mode is required");
            }

            string previousMode = GetCurrentPlayModeString();
            string targetMode = req.mode.ToLowerInvariant();

            switch (targetMode)
            {
                case "play":
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = true;
                    }
                    else if (EditorApplication.isPaused)
                    {
                        EditorApplication.isPaused = false;
                    }
                    break;

                case "pause":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPaused = true;
                    }
                    else
                    {
                        return RpcResponse.Failure("INVALID_STATE", "Cannot pause when not playing");
                    }
                    break;

                case "stop":
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = false;
                    }
                    break;

                default:
                    return RpcResponse.Failure("INVALID_PARAMS", $"Invalid mode: {req.mode}. Must be 'play', 'pause', or 'stop'");
            }

            return RpcResponse.Success(new SetPlayModeResponse
            {
                success = true,
                previousMode = previousMode,
                currentMode = targetMode
            });
        }

        private static string GetCurrentPlayModeString()
        {
            if (!EditorApplication.isPlaying) return "stop";
            if (EditorApplication.isPaused) return "pause";
            return "play";
        }

        public static RpcResponse GetActiveContext(RpcRequest request)
        {
            var req = request.GetParams<GetActiveContextRequest>();
            var response = new GetActiveContextResponse();

            // Play mode state
            if (req.includePlayModeState)
            {
                response.playModeState = GetCurrentPlayModeString();
                response.isCompiling = EditorApplication.isCompiling;
                
                var activeScene = SceneManager.GetActiveScene();
                response.activeScene = activeScene.IsValid() ? activeScene.name : "";
            }

            // Selection
            if (req.includeSelection)
            {
                var selectedObjects = Selection.gameObjects;
                if (selectedObjects != null && selectedObjects.Length > 0)
                {
                    response.selectedObjects = new List<SelectedObjectInfo>();
                    
                    foreach (var go in selectedObjects)
                    {
                        if (go == null) continue;
                        
                        var info = new SelectedObjectInfo
                        {
                            name = go.name,
                            path = GameObjectHandlers.GetGameObjectPath(go),
                            type = "GameObject",
                            components = go.GetComponents<Component>()
                                .Where(c => c != null)
                                .Select(c => c.GetType().Name)
                                .ToList()
                        };
                        
                        response.selectedObjects.Add(info);
                    }

                    // Primary selection
                    if (response.selectedObjects.Count > 0)
                    {
                        response.selectedObject = response.selectedObjects[0];
                    }
                }
            }

            // Recent console errors
            response.recentErrors = ConsoleHandlers.GetRecentErrors(req.maxConsoleErrors);

            return RpcResponse.Success(response);
        }

        // ====================================================================
        // Recompile & Refresh (Phase 44 + Phase 50 enhancements)
        // ====================================================================

        // Compilation tracking
        private static List<CompilerMessage> _compilationMessages = new List<CompilerMessage>();
        private static int _processedAssemblies = 0;
        private static bool _isTrackingCompilation = false;
        private static DateTime _compilationStartTime;

        [Serializable]
        public class RecompileRequest
        {
            public bool returnWithLogs = true;
            public int logsLimit = 100;
        }

        [Serializable]
        public class CompilerMessageInfo
        {
            public string type;
            public string message;
            public string file;
            public int line;
            public int column;
        }

        [Serializable]
        public class RecompileResponse
        {
            public bool success;
            public string message;
            public bool isCompiling;
            public int assembliesProcessed;
            public int errorCount;
            public int warningCount;
            public List<CompilerMessageInfo> logs;
        }

        /// <summary>
        /// Force script recompilation with optional message tracking
        /// </summary>
        public static RpcResponse RecompileScripts(RpcRequest request)
        {
            try
            {
                var req = request.GetParams<RecompileRequest>();
                
                // Start tracking compilation messages
                if (req.returnWithLogs && !_isTrackingCompilation)
                {
                    StartCompilationTracking();
                }

                // Request script compilation
                CompilationPipeline.RequestScriptCompilation();
                
                return RpcResponse.Success(new RecompileResponse
                {
                    success = true,
                    message = "Script recompilation requested. Use get_compilation_status to check results.",
                    isCompiling = true,
                    assembliesProcessed = 0,
                    errorCount = 0,
                    warningCount = 0
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("RECOMPILE_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Get the status and results of the last compilation
        /// </summary>
        public static RpcResponse GetCompilationStatus(RpcRequest request)
        {
            try
            {
                var req = request.GetParams<RecompileRequest>();
                int logsLimit = req?.logsLimit ?? 100;

                var response = new RecompileResponse
                {
                    success = true,
                    isCompiling = EditorApplication.isCompiling,
                    assembliesProcessed = _processedAssemblies
                };

                if (_compilationMessages.Count > 0)
                {
                    response.errorCount = _compilationMessages.Count(m => m.type == CompilerMessageType.Error);
                    response.warningCount = _compilationMessages.Count(m => m.type == CompilerMessageType.Warning);
                    
                    // Sort: errors first, then warnings, then info
                    var sortedMessages = _compilationMessages
                        .OrderBy(m => m.type)
                        .Take(logsLimit)
                        .Select(m => new CompilerMessageInfo
                        {
                            type = m.type.ToString(),
                            message = m.message,
                            file = m.file,
                            line = m.line,
                            column = m.column
                        })
                        .ToList();

                    response.logs = sortedMessages;
                    response.message = response.errorCount > 0 
                        ? $"Compilation completed with {response.errorCount} error(s) and {response.warningCount} warning(s)"
                        : $"Compilation completed successfully with {response.warningCount} warning(s)";
                }
                else
                {
                    response.message = EditorApplication.isCompiling 
                        ? "Compilation in progress..." 
                        : "No compilation data available";
                    response.logs = new List<CompilerMessageInfo>();
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("STATUS_ERROR", ex.Message);
            }
        }

        private static void StartCompilationTracking()
        {
            _compilationMessages.Clear();
            _processedAssemblies = 0;
            _isTrackingCompilation = true;
            _compilationStartTime = DateTime.Now;
            
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
        }

        private static void StopCompilationTracking()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            _isTrackingCompilation = false;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            _processedAssemblies++;
            _compilationMessages.AddRange(messages);
        }

        private static void OnCompilationFinished(object context)
        {
            var duration = DateTime.Now - _compilationStartTime;
            Debug.Log($"[EditorHandlers] Compilation finished in {duration.TotalSeconds:F2}s. " +
                      $"Processed {_processedAssemblies} assemblies with {_compilationMessages.Count} messages " +
                      $"({_compilationMessages.Count(m => m.type == CompilerMessageType.Error)} errors, " +
                      $"{_compilationMessages.Count(m => m.type == CompilerMessageType.Warning)} warnings)");
            
            StopCompilationTracking();
        }

        /// <summary>
        /// Refresh the AssetDatabase
        /// </summary>
        public static RpcResponse RefreshAssets(RpcRequest request)
        {
            try
            {
                AssetDatabase.Refresh();
                
                return RpcResponse.Success(new
                {
                    success = true,
                    message = "Asset database refreshed"
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("REFRESH_ERROR", ex.Message);
            }
        }
    }
}
