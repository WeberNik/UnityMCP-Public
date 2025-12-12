// ============================================================================
// UnityVision Bridge - Scene Handlers
// Handlers for scene listing and hierarchy inspection
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class SceneHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class ListScenesRequest
        {
            public string filter = "";
        }

        [Serializable]
        public class SceneInfo
        {
            public string name;
            public string path;
            public bool isLoaded;
            public bool isActive;
            public int buildIndex;
        }

        [Serializable]
        public class ListScenesResponse
        {
            public List<SceneInfo> scenes;
        }

        [Serializable]
        public class GetSceneHierarchyRequest
        {
            public string sceneName = "";
            public string rootPath = "";  // Start from a specific GameObject path instead of scene root
            public int maxDepth = 5;
            public bool includeComponents = false;
            public string nameFilter = "";
            public int maxObjects = 500;  // Limit total objects returned
        }

        [Serializable]
        public class GameObjectNode
        {
            public string id;
            public string name;
            public string path;
            public bool active;
            public List<string> components;
            public List<GameObjectNode> children;
        }

        [Serializable]
        public class GetSceneHierarchyResponse
        {
            public List<GameObjectNode> rootObjects;
            public int totalObjectsInScene;
            public int returnedObjects;
            public bool truncated;
        }

        #endregion

        public static RpcResponse ListScenes(RpcRequest request)
        {
            var req = request.GetParams<ListScenesRequest>();
            var scenes = new List<SceneInfo>();

            // Get all scenes in build settings
            var buildScenes = EditorBuildSettings.scenes;
            var activeScene = SceneManager.GetActiveScene();

            foreach (var buildScene in buildScenes)
            {
                var sceneName = System.IO.Path.GetFileNameWithoutExtension(buildScene.path);

                // Apply filter
                if (!string.IsNullOrEmpty(req.filter) &&
                    !sceneName.Contains(req.filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Check if loaded
                bool isLoaded = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var loadedScene = SceneManager.GetSceneAt(i);
                    if (loadedScene.path == buildScene.path && loadedScene.isLoaded)
                    {
                        isLoaded = true;
                        break;
                    }
                }

                scenes.Add(new SceneInfo
                {
                    name = sceneName,
                    path = buildScene.path,
                    isLoaded = isLoaded,
                    isActive = activeScene.path == buildScene.path,
                    buildIndex = Array.IndexOf(buildScenes, buildScene)
                });
            }

            return RpcResponse.Success(new ListScenesResponse { scenes = scenes });
        }

        public static RpcResponse GetSceneHierarchy(RpcRequest request)
        {
            var req = request.GetParams<GetSceneHierarchyRequest>();

            Scene targetScene;
            if (string.IsNullOrEmpty(req.sceneName))
            {
                targetScene = SceneManager.GetActiveScene();
            }
            else
            {
                targetScene = SceneManager.GetSceneByName(req.sceneName);
                if (!targetScene.IsValid())
                {
                    return RpcResponse.Failure("SCENE_NOT_FOUND", $"Scene '{req.sceneName}' not found or not loaded");
                }
            }

            // Count total objects in scene
            int totalObjects = 0;
            foreach (var rootGo in targetScene.GetRootGameObjects())
            {
                totalObjects += CountGameObjects(rootGo);
            }

            var rootObjects = new List<GameObjectNode>();
            int objectCount = 0;
            int maxObjects = Math.Max(1, req.maxObjects);

            // If rootPath is specified, start from that object
            if (!string.IsNullOrEmpty(req.rootPath))
            {
                var rootGo = GameObjectHandlers.FindGameObjectByPath(req.rootPath);
                if (rootGo == null)
                {
                    return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"Root GameObject not found at path: {req.rootPath}");
                }

                var node = BuildGameObjectNodeWithLimit(rootGo, GetParentPath(rootGo), req.maxDepth, 0, 
                    req.includeComponents, req.nameFilter, ref objectCount, maxObjects);
                if (node != null)
                {
                    rootObjects.Add(node);
                }
            }
            else
            {
                // Start from scene roots
                foreach (var go in targetScene.GetRootGameObjects())
                {
                    if (objectCount >= maxObjects) break;
                    
                    var node = BuildGameObjectNodeWithLimit(go, "", req.maxDepth, 0, 
                        req.includeComponents, req.nameFilter, ref objectCount, maxObjects);
                    if (node != null)
                    {
                        rootObjects.Add(node);
                    }
                }
            }

            return RpcResponse.Success(new GetSceneHierarchyResponse 
            { 
                rootObjects = rootObjects,
                totalObjectsInScene = totalObjects,
                returnedObjects = objectCount,
                truncated = objectCount >= maxObjects
            });
        }

        private static int CountGameObjects(GameObject go)
        {
            int count = 1;
            foreach (Transform child in go.transform)
            {
                count += CountGameObjects(child.gameObject);
            }
            return count;
        }

        private static string GetParentPath(GameObject go)
        {
            if (go.transform.parent == null) return "";
            return GameObjectHandlers.GetGameObjectPath(go.transform.parent.gameObject);
        }

        private static GameObjectNode BuildGameObjectNode(
            GameObject go,
            string parentPath,
            int maxDepth,
            int currentDepth,
            bool includeComponents,
            string nameFilter)
        {
            int dummy = 0;
            return BuildGameObjectNodeWithLimit(go, parentPath, maxDepth, currentDepth, 
                includeComponents, nameFilter, ref dummy, int.MaxValue);
        }

        private static GameObjectNode BuildGameObjectNodeWithLimit(
            GameObject go,
            string parentPath,
            int maxDepth,
            int currentDepth,
            bool includeComponents,
            string nameFilter,
            ref int objectCount,
            int maxObjects)
        {
            if (objectCount >= maxObjects) return null;

            string path = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";

            // Check name filter
            bool matchesFilter = string.IsNullOrEmpty(nameFilter) ||
                                 go.name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase);

            // Build children first to check if any match
            List<GameObjectNode> children = null;
            if (currentDepth < maxDepth && objectCount < maxObjects)
            {
                children = new List<GameObjectNode>();
                foreach (Transform child in go.transform)
                {
                    if (objectCount >= maxObjects) break;
                    
                    var childNode = BuildGameObjectNodeWithLimit(
                        child.gameObject, path, maxDepth, currentDepth + 1, 
                        includeComponents, nameFilter, ref objectCount, maxObjects);
                    if (childNode != null)
                    {
                        children.Add(childNode);
                    }
                }
            }

            // Include this node if it matches filter or has matching children
            if (!matchesFilter && (children == null || children.Count == 0))
            {
                return null;
            }

            objectCount++;

            var node = new GameObjectNode
            {
                id = $"go_{go.GetInstanceID()}",
                name = go.name,
                path = path,
                active = go.activeSelf,
                children = children
            };

            if (includeComponents)
            {
                node.components = go.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().FullName)
                    .ToList();
            }

            return node;
        }

        // ====================================================================
        // Scene CRUD Operations (Phase 43)
        // ====================================================================

        [Serializable]
        public class SceneCreateRequest
        {
            public string path;
            public string template;
        }

        [Serializable]
        public class SceneLoadRequest
        {
            public string path;
            public bool additive;
        }

        [Serializable]
        public class SceneSaveRequest
        {
            public string path;
            public string saveAs;
        }

        [Serializable]
        public class SceneDeleteRequest
        {
            public string path;
        }

        /// <summary>
        /// Create a new scene
        /// </summary>
        public static RpcResponse CreateScene(RpcRequest request)
        {
            try
            {
                var req = request.GetParams<SceneCreateRequest>();
                
                if (string.IsNullOrEmpty(req.path))
                {
                    return RpcResponse.Failure("INVALID_PARAMS", "path is required");
                }

                // Ensure path ends with .unity
                string scenePath = req.path;
                if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    scenePath += ".unity";
                }

                // Ensure path starts with Assets/
                if (!scenePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    scenePath = "Assets/" + scenePath;
                }

                // Create new scene
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                
                // Save the scene
                bool saved = EditorSceneManager.SaveScene(newScene, scenePath);
                
                if (!saved)
                {
                    return RpcResponse.Failure("SAVE_FAILED", $"Failed to save scene to {scenePath}");
                }

                return RpcResponse.Success(new
                {
                    success = true,
                    path = scenePath,
                    name = System.IO.Path.GetFileNameWithoutExtension(scenePath)
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("CREATE_SCENE_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Load a scene
        /// </summary>
        public static RpcResponse LoadScene(RpcRequest request)
        {
            try
            {
                var req = request.GetParams<SceneLoadRequest>();
                
                if (string.IsNullOrEmpty(req.path))
                {
                    return RpcResponse.Failure("INVALID_PARAMS", "path is required");
                }

                // Ensure path starts with Assets/
                string scenePath = req.path;
                if (!scenePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    scenePath = "Assets/" + scenePath;
                }

                // Check if scene exists
                if (!System.IO.File.Exists(scenePath))
                {
                    return RpcResponse.Failure("SCENE_NOT_FOUND", $"Scene not found: {scenePath}");
                }

                // Load the scene
                var mode = req.additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
                var scene = EditorSceneManager.OpenScene(scenePath, mode);

                return RpcResponse.Success(new
                {
                    success = true,
                    path = scenePath,
                    name = scene.name,
                    isLoaded = scene.isLoaded,
                    additive = req.additive
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("LOAD_SCENE_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Save the current scene
        /// </summary>
        public static RpcResponse SaveScene(RpcRequest request)
        {
            try
            {
                var req = request.GetParams<SceneSaveRequest>();
                
                var activeScene = SceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                {
                    return RpcResponse.Failure("NO_ACTIVE_SCENE", "No active scene to save");
                }

                string savePath = activeScene.path;
                
                // Save as new path if specified
                if (!string.IsNullOrEmpty(req.saveAs))
                {
                    savePath = req.saveAs;
                    if (!savePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        savePath += ".unity";
                    }
                    if (!savePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    {
                        savePath = "Assets/" + savePath;
                    }
                }
                else if (!string.IsNullOrEmpty(req.path))
                {
                    savePath = req.path;
                }

                bool saved = EditorSceneManager.SaveScene(activeScene, savePath);
                
                if (!saved)
                {
                    return RpcResponse.Failure("SAVE_FAILED", $"Failed to save scene to {savePath}");
                }

                return RpcResponse.Success(new
                {
                    success = true,
                    path = savePath,
                    name = activeScene.name
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("SAVE_SCENE_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Delete a scene file
        /// </summary>
        public static RpcResponse DeleteScene(RpcRequest request)
        {
            try
            {
                var req = request.GetParams<SceneDeleteRequest>();
                
                if (string.IsNullOrEmpty(req.path))
                {
                    return RpcResponse.Failure("INVALID_PARAMS", "path is required");
                }

                // Ensure path starts with Assets/
                string scenePath = req.path;
                if (!scenePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    scenePath = "Assets/" + scenePath;
                }

                // Check if scene exists
                if (!System.IO.File.Exists(scenePath))
                {
                    return RpcResponse.Failure("SCENE_NOT_FOUND", $"Scene not found: {scenePath}");
                }

                // Check if scene is currently loaded
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.path == scenePath)
                {
                    return RpcResponse.Failure("SCENE_IN_USE", "Cannot delete the currently active scene");
                }

                // Check if scene is in build settings
                bool inBuildSettings = false;
                foreach (var buildScene in EditorBuildSettings.scenes)
                {
                    if (buildScene.path == scenePath)
                    {
                        inBuildSettings = true;
                        break;
                    }
                }

                // Delete the scene
                bool deleted = AssetDatabase.DeleteAsset(scenePath);
                
                if (!deleted)
                {
                    return RpcResponse.Failure("DELETE_FAILED", $"Failed to delete scene: {scenePath}");
                }

                return RpcResponse.Success(new
                {
                    success = true,
                    path = scenePath,
                    wasInBuildSettings = inBuildSettings,
                    warning = inBuildSettings ? "Scene was in build settings and may need to be removed manually" : null
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("DELETE_SCENE_ERROR", ex.Message);
            }
        }
    }
}
