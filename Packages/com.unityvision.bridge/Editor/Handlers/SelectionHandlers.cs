// ============================================================================
// UnityVision Bridge - Selection Handlers
// Handlers for editor selection sync - get/set what user has selected
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class SelectionHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetSelectionResponse
        {
            public int gameObjectCount;
            public int assetCount;
            public List<SelectedGameObject> gameObjects;
            public List<SelectedAsset> assets;
            public string activeObjectPath;
            public string activeObjectType;
        }

        [Serializable]
        public class SelectedGameObject
        {
            public string path;
            public string name;
            public List<string> components;
            public string tag;
            public string layer;
            public bool isActive;
            public bool isPrefabInstance;
        }

        [Serializable]
        public class SelectedAsset
        {
            public string path;
            public string name;
            public string type;
            public long sizeBytes;
        }

        [Serializable]
        public class SetSelectionRequest
        {
            public List<string> gameObjectPaths;
            public List<string> assetPaths;
            public bool frameInSceneView = true;
            public bool focusInHierarchy = true;
        }

        [Serializable]
        public class SetSelectionResponse
        {
            public bool success;
            public int selectedCount;
            public List<string> notFound;
        }

        #endregion

        public static RpcResponse GetEditorSelection(RpcRequest request)
        {
            try
            {
                var response = new GetSelectionResponse
                {
                    gameObjects = new List<SelectedGameObject>(),
                    assets = new List<SelectedAsset>()
                };

                // Get selected GameObjects
                var selectedGOs = Selection.gameObjects;
                response.gameObjectCount = selectedGOs.Length;

                foreach (var go in selectedGOs)
                {
                    var selGo = new SelectedGameObject
                    {
                        path = GetGameObjectPath(go),
                        name = go.name,
                        components = go.GetComponents<Component>()
                            .Where(c => c != null)
                            .Select(c => c.GetType().Name)
                            .ToList(),
                        tag = go.tag,
                        layer = LayerMask.LayerToName(go.layer),
                        isActive = go.activeInHierarchy,
                        isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go)
                    };
                    response.gameObjects.Add(selGo);
                }

                // Get selected assets (non-scene objects)
                var selectedObjects = Selection.objects;
                foreach (var obj in selectedObjects)
                {
                    if (obj is GameObject) continue; // Already handled
                    
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var fileInfo = new System.IO.FileInfo(assetPath);
                    response.assets.Add(new SelectedAsset
                    {
                        path = assetPath,
                        name = obj.name,
                        type = obj.GetType().Name,
                        sizeBytes = fileInfo.Exists ? fileInfo.Length : 0
                    });
                }
                response.assetCount = response.assets.Count;

                // Active object info
                if (Selection.activeObject != null)
                {
                    response.activeObjectType = Selection.activeObject.GetType().Name;
                    if (Selection.activeGameObject != null)
                    {
                        response.activeObjectPath = GetGameObjectPath(Selection.activeGameObject);
                    }
                    else
                    {
                        response.activeObjectPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                    }
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("SELECTION_ERROR", ex.Message);
            }
        }

        public static RpcResponse SetEditorSelection(RpcRequest request)
        {
            var req = request.GetParams<SetSelectionRequest>();

            try
            {
                var objectsToSelect = new List<UnityEngine.Object>();
                var notFound = new List<string>();

                // Find GameObjects by path
                if (req.gameObjectPaths != null)
                {
                    foreach (var path in req.gameObjectPaths)
                    {
                        var go = GameObjectHandlers.FindGameObjectByPath(path);
                        if (go != null)
                        {
                            objectsToSelect.Add(go);
                        }
                        else
                        {
                            notFound.Add(path);
                        }
                    }
                }

                // Find assets by path
                if (req.assetPaths != null)
                {
                    foreach (var path in req.assetPaths)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (asset != null)
                        {
                            objectsToSelect.Add(asset);
                        }
                        else
                        {
                            notFound.Add(path);
                        }
                    }
                }

                // Set selection
                Selection.objects = objectsToSelect.ToArray();

                // Frame in scene view if requested and we have GameObjects
                if (req.frameInSceneView && objectsToSelect.Any(o => o is GameObject))
                {
                    SceneView.lastActiveSceneView?.FrameSelected();
                }

                // Focus hierarchy window if requested
                if (req.focusInHierarchy && objectsToSelect.Any(o => o is GameObject))
                {
                    EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
                }

                return RpcResponse.Success(new SetSelectionResponse
                {
                    success = true,
                    selectedCount = objectsToSelect.Count,
                    notFound = notFound
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("SELECTION_ERROR", ex.Message);
            }
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
    }
}
