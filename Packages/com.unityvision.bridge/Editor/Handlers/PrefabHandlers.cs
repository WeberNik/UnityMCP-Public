// ============================================================================
// UnityVision Bridge - Prefab Handlers
// Handlers for prefab inspection, overrides, and instance management
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
    public static class PrefabHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetPrefabOverridesRequest
        {
            public string gameObjectPath;
        }

        [Serializable]
        public class GetPrefabOverridesResponse
        {
            public bool isPrefabInstance;
            public string prefabAssetPath;
            public string prefabName;
            public List<PropertyOverride> propertyOverrides;
            public List<string> addedComponents;
            public List<string> removedComponents;
            public List<string> addedGameObjects;
            public List<string> removedGameObjects;
            public int totalOverrides;
        }

        [Serializable]
        public class PropertyOverride
        {
            public string objectPath;
            public string componentType;
            public string propertyPath;
            public string currentValue;
            public string prefabValue;
        }

        [Serializable]
        public class ApplyRevertRequest
        {
            public string gameObjectPath;
            public bool applyAll = true;
            public List<string> specificProperties;  // If not applyAll, which properties
        }

        [Serializable]
        public class ApplyRevertResponse
        {
            public bool success;
            public int changesApplied;
        }

        [Serializable]
        public class FindPrefabInstancesRequest
        {
            public string prefabPath;
            public bool includeNestedPrefabs = true;
        }

        [Serializable]
        public class FindPrefabInstancesResponse
        {
            public int totalInstances;
            public List<PrefabInstanceInfo> instances;
        }

        [Serializable]
        public class PrefabInstanceInfo
        {
            public string path;
            public string scenePath;
            public bool hasOverrides;
            public int overrideCount;
        }

        #endregion

        public static RpcResponse GetPrefabOverrides(RpcRequest request)
        {
            var req = request.GetParams<GetPrefabOverridesRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                var response = new GetPrefabOverridesResponse
                {
                    isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go),
                    propertyOverrides = new List<PropertyOverride>(),
                    addedComponents = new List<string>(),
                    removedComponents = new List<string>(),
                    addedGameObjects = new List<string>(),
                    removedGameObjects = new List<string>()
                };

                if (!response.isPrefabInstance)
                {
                    return RpcResponse.Success(response);
                }

                // Get prefab asset
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (prefabAsset != null)
                {
                    response.prefabAssetPath = AssetDatabase.GetAssetPath(prefabAsset);
                    response.prefabName = prefabAsset.name;
                }

                // Get property modifications
                var modifications = PrefabUtility.GetPropertyModifications(go);
                if (modifications != null)
                {
                    foreach (var mod in modifications)
                    {
                        if (mod.target == null) continue;

                        // Skip internal properties
                        if (mod.propertyPath.StartsWith("m_Father") ||
                            mod.propertyPath.StartsWith("m_RootOrder") ||
                            mod.propertyPath.StartsWith("m_LocalPosition") ||
                            mod.propertyPath.StartsWith("m_LocalRotation") ||
                            mod.propertyPath.StartsWith("m_LocalScale"))
                        {
                            // Only skip transform if it's the root
                            if (mod.target is Transform t && t.gameObject == go)
                                continue;
                        }

                        string componentType = mod.target.GetType().Name;
                        string objectPath = mod.target is Component c ? GetGameObjectPath(c.gameObject) : "";

                        // Get prefab value
                        string prefabValue = "";
                        var prefabComponent = PrefabUtility.GetCorrespondingObjectFromSource(mod.target);
                        if (prefabComponent != null)
                        {
                            var so = new SerializedObject(prefabComponent);
                            var prop = so.FindProperty(mod.propertyPath);
                            if (prop != null)
                            {
                                prefabValue = GetPropertyValueString(prop);
                            }
                        }

                        response.propertyOverrides.Add(new PropertyOverride
                        {
                            objectPath = objectPath,
                            componentType = componentType,
                            propertyPath = mod.propertyPath,
                            currentValue = mod.value,
                            prefabValue = prefabValue
                        });
                    }
                }

                // Get added components
                var addedComps = PrefabUtility.GetAddedComponents(go);
                foreach (var added in addedComps)
                {
                    response.addedComponents.Add($"{GetGameObjectPath(added.instanceComponent.gameObject)}: {added.instanceComponent.GetType().Name}");
                }

                // Get removed components
                var removedComps = PrefabUtility.GetRemovedComponents(go);
                foreach (var removed in removedComps)
                {
                    response.removedComponents.Add($"{removed.assetComponent.GetType().Name}");
                }

                // Get added GameObjects
                var addedGOs = PrefabUtility.GetAddedGameObjects(go);
                foreach (var added in addedGOs)
                {
                    response.addedGameObjects.Add(GetGameObjectPath(added.instanceGameObject));
                }

                // Get removed GameObjects (this is trickier, need to compare with prefab)
                // For now, we'll skip this as it requires more complex comparison

                response.totalOverrides = response.propertyOverrides.Count + 
                                         response.addedComponents.Count + 
                                         response.removedComponents.Count +
                                         response.addedGameObjects.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PREFAB_ERROR", ex.Message);
            }
        }

        public static RpcResponse ApplyPrefabOverrides(RpcRequest request)
        {
            var req = request.GetParams<ApplyRevertRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                if (!PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    return RpcResponse.Failure("NOT_PREFAB", "GameObject is not a prefab instance");
                }

                // Get the root of the prefab instance
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);

                if (req.applyAll)
                {
                    PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                }
                else
                {
                    // Apply specific overrides
                    // This is more complex and would require matching specific modifications
                    // For now, we'll apply all
                    PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                }

                return RpcResponse.Success(new ApplyRevertResponse
                {
                    success = true,
                    changesApplied = 1  // We applied all at once
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PREFAB_ERROR", ex.Message);
            }
        }

        public static RpcResponse RevertPrefabOverrides(RpcRequest request)
        {
            var req = request.GetParams<ApplyRevertRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                if (!PrefabUtility.IsPartOfPrefabInstance(go))
                {
                    return RpcResponse.Failure("NOT_PREFAB", "GameObject is not a prefab instance");
                }

                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);

                if (req.applyAll)
                {
                    PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
                }
                else
                {
                    // Revert specific properties
                    PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
                }

                return RpcResponse.Success(new ApplyRevertResponse
                {
                    success = true,
                    changesApplied = 1
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PREFAB_ERROR", ex.Message);
            }
        }

        public static RpcResponse FindPrefabInstances(RpcRequest request)
        {
            var req = request.GetParams<FindPrefabInstancesRequest>();

            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(req.prefabPath);
                if (prefab == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Prefab not found: {req.prefabPath}");
                }

                var response = new FindPrefabInstancesResponse
                {
                    instances = new List<PrefabInstanceInfo>()
                };

                // Search in all loaded scenes
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;

                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        SearchForPrefabInstances(root, prefab, scene.path, response.instances, req.includeNestedPrefabs);
                    }
                }

                response.totalInstances = response.instances.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PREFAB_ERROR", ex.Message);
            }
        }

        #region Helper Methods

        private static void SearchForPrefabInstances(GameObject go, GameObject prefab, string scenePath, 
            List<PrefabInstanceInfo> results, bool includeNested)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source == prefab || (includeNested && IsNestedPrefabOf(source, prefab)))
                {
                    var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                    if (root == go) // Only count roots
                    {
                        var mods = PrefabUtility.GetPropertyModifications(go);
                        results.Add(new PrefabInstanceInfo
                        {
                            path = GetGameObjectPath(go),
                            scenePath = scenePath,
                            hasOverrides = mods != null && mods.Length > 0,
                            overrideCount = mods?.Length ?? 0
                        });
                    }
                }
            }

            // Search children
            foreach (Transform child in go.transform)
            {
                SearchForPrefabInstances(child.gameObject, prefab, scenePath, results, includeNested);
            }
        }

        private static bool IsNestedPrefabOf(UnityEngine.Object source, GameObject prefab)
        {
            if (source == null) return false;
            
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            
            // Check if source is part of the prefab hierarchy
            return sourcePath == prefabPath;
        }

        private static string GetGameObjectPath(GameObject go)
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

        private static string GetPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F4");
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.ObjectReference: 
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "None";
                default: return $"[{prop.propertyType}]";
            }
        }

        #endregion
    }
}
