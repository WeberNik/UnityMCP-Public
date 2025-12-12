// ============================================================================
// UnityVision Bridge - GameObject Handlers
// Handlers for creating, modifying, and deleting GameObjects
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
    public static class GameObjectHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class CreateGameObjectRequest
        {
            public string sceneName = "";
            public string parentPath = "";
            public string name;
            public List<ComponentSpec> components;
            public Vector3Data position;
            public Vector3Data rotation;
            public Vector3Data scale;
            public bool dryRun = false;
        }

        [Serializable]
        public class CreateGameObjectResponse
        {
            public bool success;
            public string id;
            public string path;
            public object dryRunPlan;
        }

        [Serializable]
        public class ModifyGameObjectRequest
        {
            public string path;
            public string newName;
            public string parentPath;
            public TransformData transform;
            public bool? active;
            public bool dryRun = false;
        }

        [Serializable]
        public class ModifyGameObjectResponse
        {
            public bool success;
            public object dryRunPlan;
        }

        [Serializable]
        public class DeleteGameObjectRequest
        {
            public string path;
            public bool confirm = false;
            public bool dryRun = false;
        }

        [Serializable]
        public class DeleteGameObjectResponse
        {
            public bool success;
            public object dryRunPlan;
        }

        #endregion

        public static RpcResponse CreateGameObject(RpcRequest request)
        {
            var req = request.GetParams<CreateGameObjectRequest>();

            if (string.IsNullOrEmpty(req.name))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "name is required");
            }

            // Find parent if specified
            GameObject parent = null;
            if (!string.IsNullOrEmpty(req.parentPath))
            {
                parent = FindGameObjectByPath(req.parentPath);
                if (parent == null)
                {
                    return RpcResponse.Failure("PARENT_NOT_FOUND", $"Parent GameObject not found at path: {req.parentPath}");
                }
            }

            string finalPath = string.IsNullOrEmpty(req.parentPath) ? req.name : $"{req.parentPath}/{req.name}";
            var componentTypes = req.components?.Select(c => c.Type).ToList() ?? new List<string>();

            // Dry run - just return plan
            if (req.dryRun)
            {
                return RpcResponse.Success(new CreateGameObjectResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldCreate = req.name,
                        at = finalPath,
                        withComponents = componentTypes
                    }
                });
            }

            // Create the GameObject
            var go = new GameObject(req.name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {req.name}");

            // Set parent
            if (parent != null)
            {
                Undo.SetTransformParent(go.transform, parent.transform, $"Set parent of {req.name}");
            }

            // Set transform
            if (req.position != null)
            {
                go.transform.localPosition = req.position.ToVector3();
            }
            if (req.rotation != null)
            {
                go.transform.localEulerAngles = req.rotation.ToVector3();
            }
            if (req.scale != null)
            {
                go.transform.localScale = req.scale.ToVector3();
            }

            // Add components
            if (req.components != null)
            {
                foreach (var comp in req.components)
                {
                    var type = FindType(comp.Type);
                    if (type != null)
                    {
                        Undo.AddComponent(go, type);
                    }
                }
            }

            // Mark scene dirty
            EditorSceneManager.MarkSceneDirty(go.scene);

            return RpcResponse.Success(new CreateGameObjectResponse
            {
                success = true,
                id = $"go_{go.GetInstanceID()}",
                path = GetGameObjectPath(go)
            });
        }

        public static RpcResponse ModifyGameObject(RpcRequest request)
        {
            var req = request.GetParams<ModifyGameObjectRequest>();

            if (string.IsNullOrEmpty(req.path))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "path is required");
            }

            var go = FindGameObjectByPath(req.path);
            if (go == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"GameObject not found at path: {req.path}");
            }

            // Ping the object so user can see what's being modified
            PingGameObject(go);

            // Build changes summary
            var changes = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(req.newName)) changes["name"] = req.newName;
            if (req.parentPath != null) changes["parentPath"] = req.parentPath;
            if (req.transform != null) changes["transform"] = req.transform;
            if (req.active.HasValue) changes["active"] = req.active.Value;

            // Dry run
            if (req.dryRun)
            {
                return RpcResponse.Success(new ModifyGameObjectResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldModify = req.path,
                        changes = changes
                    }
                });
            }

            // Apply changes
            Undo.RecordObject(go, $"Modify {go.name}");
            Undo.RecordObject(go.transform, $"Modify {go.name} transform");

            if (!string.IsNullOrEmpty(req.newName))
            {
                go.name = req.newName;
            }

            if (req.parentPath != null)
            {
                if (string.IsNullOrEmpty(req.parentPath))
                {
                    // Move to root
                    Undo.SetTransformParent(go.transform, null, $"Move {go.name} to root");
                }
                else
                {
                    var newParent = FindGameObjectByPath(req.parentPath);
                    if (newParent == null)
                    {
                        return RpcResponse.Failure("PARENT_NOT_FOUND", $"New parent not found: {req.parentPath}");
                    }
                    Undo.SetTransformParent(go.transform, newParent.transform, $"Reparent {go.name}");
                }
            }

            if (req.transform != null)
            {
                if (req.transform.Position != null)
                    go.transform.localPosition = req.transform.Position.ToVector3();
                if (req.transform.Rotation != null)
                    go.transform.localEulerAngles = req.transform.Rotation.ToVector3();
                if (req.transform.Scale != null)
                    go.transform.localScale = req.transform.Scale.ToVector3();
            }

            if (req.active.HasValue)
            {
                go.SetActive(req.active.Value);
            }

            EditorSceneManager.MarkSceneDirty(go.scene);

            return RpcResponse.Success(new ModifyGameObjectResponse { success = true });
        }

        public static RpcResponse DeleteGameObject(RpcRequest request)
        {
            var req = request.GetParams<DeleteGameObjectRequest>();

            if (string.IsNullOrEmpty(req.path))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "path is required");
            }

            var go = FindGameObjectByPath(req.path);
            if (go == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"GameObject not found at path: {req.path}");
            }

            // Ping the object so user can see what's being deleted
            PingGameObject(go);

            int childCount = go.transform.GetComponentsInChildren<Transform>(true).Length - 1;

            // Dry run
            if (req.dryRun)
            {
                return RpcResponse.Success(new DeleteGameObjectResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldDelete = req.path,
                        childCount = childCount
                    }
                });
            }

            // Require confirmation for actual delete
            if (!req.confirm)
            {
                return RpcResponse.Failure("CONFIRMATION_REQUIRED",
                    $"Set confirm=true to delete '{req.path}' and its {childCount} children");
            }

            var scene = go.scene;
            Undo.DestroyObjectImmediate(go);
            EditorSceneManager.MarkSceneDirty(scene);

            return RpcResponse.Success(new DeleteGameObjectResponse { success = true });
        }

        #region Helpers

        /// <summary>
        /// Find a GameObject by path or ID. Supports:
        /// - "go_-3642" format (instance ID)
        /// - "Parent/Child/Target" format (hierarchy path)
        /// </summary>
        /// <param name="pathOrId">Path or ID string</param>
        /// <param name="autoCreate">If true, creates missing GameObjects in the path</param>
        public static GameObject FindGameObjectByPath(string pathOrId, bool autoCreate = false)
        {
            if (string.IsNullOrEmpty(pathOrId)) return null;

            // Check if it's a go_ ID format (e.g., "go_-3642")
            if (pathOrId.StartsWith("go_"))
            {
                return FindGameObjectById(pathOrId);
            }

            // Check if it's a prefab path
            if (pathOrId.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                // Return null - caller should use AssetHandlers for prefabs
                return null;
            }

            // Otherwise treat as path
            var parts = pathOrId.Split('/');
            GameObject current = null;

            // Find root object
            foreach (var rootGo in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootGo.name == parts[0])
                {
                    current = rootGo;
                    break;
                }
            }

            // Auto-create root if needed
            if (current == null)
            {
                if (autoCreate)
                {
                    current = new GameObject(parts[0]);
                    Undo.RegisterCreatedObjectUndo(current, $"Create {parts[0]}");
                }
                else
                {
                    return null;
                }
            }

            // Navigate path
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null)
                {
                    if (autoCreate)
                    {
                        // Auto-create missing child
                        var newChild = new GameObject(parts[i]);
                        Undo.RegisterCreatedObjectUndo(newChild, $"Create {parts[i]}");
                        newChild.transform.SetParent(current.transform, false);
                        current = newChild;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    current = child.gameObject;
                }
            }

            return current;
        }

        /// <summary>
        /// Find or create a GameObject hierarchy
        /// Creates all missing GameObjects in the path
        /// </summary>
        public static GameObject FindOrCreateHierarchy(string path)
        {
            return FindGameObjectByPath(path, autoCreate: true);
        }

        /// <summary>
        /// Check if a path refers to a prefab asset
        /// </summary>
        public static bool IsPrefabPath(string path)
        {
            return !string.IsNullOrEmpty(path) && 
                   path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Find a GameObject by its instance ID (format: "go_{instanceId}")
        /// </summary>
        public static GameObject FindGameObjectById(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("go_")) return null;
            
            var idStr = id.Substring(3); // Remove "go_" prefix
            if (!int.TryParse(idStr, out int instanceId)) return null;
            
            // Use EditorUtility to find object by instance ID
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            return obj as GameObject;
        }

        public static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        /// <summary>
        /// Ping a GameObject in the hierarchy window to highlight it for the user
        /// </summary>
        public static void PingGameObject(GameObject go)
        {
            if (go != null)
            {
                EditorGUIUtility.PingObject(go);
            }
        }

        /// <summary>
        /// Set tag on GameObject, auto-creating the tag if needed
        /// </summary>
        /// <param name="go">Target GameObject</param>
        /// <param name="tag">Tag to set</param>
        /// <param name="autoCreate">If true, creates the tag if it doesn't exist</param>
        /// <returns>True if tag was set successfully</returns>
        public static bool SetTagSafe(GameObject go, string tag, bool autoCreate = false)
        {
            if (go == null || string.IsNullOrEmpty(tag)) return false;

            try
            {
                go.tag = tag;
                return true;
            }
            catch (UnityException ex)
            {
                if (ex.Message.Contains("is not defined") && autoCreate)
                {
                    try
                    {
                        // Auto-create the tag
                        UnityEditorInternal.InternalEditorUtility.AddTag(tag);
                        go.tag = tag;
                        Debug.Log($"[GameObjectHandlers] Created and assigned tag '{tag}'");
                        return true;
                    }
                    catch (Exception innerEx)
                    {
                        Debug.LogWarning($"[GameObjectHandlers] Failed to create tag '{tag}': {innerEx.Message}");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"[GameObjectHandlers] Failed to set tag '{tag}': {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Validate layer value and optionally get layer name
        /// </summary>
        public static bool ValidateLayer(int layer, out string layerName)
        {
            layerName = null;
            if (layer < 0 || layer > 31) return false;
            
            layerName = LayerMask.LayerToName(layer);
            return !string.IsNullOrEmpty(layerName);
        }

        private static Type FindType(string typeName)
        {
            // Try direct lookup
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Search all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            return null;
        }

        #endregion
    }
}
