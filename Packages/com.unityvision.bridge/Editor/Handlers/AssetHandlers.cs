// ============================================================================
// UnityVision Bridge - Asset Database Handlers
// Asset search, creation, and management
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class AssetHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class SearchAssetsRequest
        {
            public string filter = "";
            public string type = "";  // e.g., "Texture2D", "Material", "Prefab"
            public string[] labels;
            public string folder = "Assets";
            public int maxResults = 100;
        }

        [Serializable]
        public class AssetInfo
        {
            public string name;
            public string path;
            public string type;
            public string guid;
            public long sizeBytes;
            public string[] labels;
        }

        [Serializable]
        public class SearchAssetsResponse
        {
            public List<AssetInfo> assets;
            public int totalCount;
        }

        [Serializable]
        public class CreateFolderRequest
        {
            public string parentFolder;
            public string folderName;
        }

        [Serializable]
        public class CreateFolderResponse
        {
            public bool success;
            public string path;
            public string guid;
        }

        [Serializable]
        public class MoveAssetRequest
        {
            public string sourcePath;
            public string destinationPath;
        }

        [Serializable]
        public class MoveAssetResponse
        {
            public bool success;
            public string newPath;
            public string error;
        }

        [Serializable]
        public class DeleteAssetRequest
        {
            public string path;
            public bool confirm = false;
        }

        [Serializable]
        public class DeleteAssetResponse
        {
            public bool success;
            public string error;
        }

        [Serializable]
        public class CreatePrefabRequest
        {
            public string gameObjectPath;
            public string savePath;
            public bool overwrite = false;
        }

        [Serializable]
        public class CreatePrefabResponse
        {
            public bool success;
            public string prefabPath;
            public string guid;
            public string error;
        }

        [Serializable]
        public class InstantiatePrefabRequest
        {
            public string prefabPath;
            public string parentPath = "";
            public Vector3Data position;
            public Vector3Data rotation;
        }

        [Serializable]
        public class InstantiatePrefabResponse
        {
            public bool success;
            public string instancePath;
            public string instanceId;
            public string error;
        }

        [Serializable]
        public class GetAssetInfoRequest
        {
            public string path;
        }

        [Serializable]
        public class GetAssetInfoResponse
        {
            public bool success;
            public AssetInfo asset;
            public Dictionary<string, object> importSettings;
            public List<string> dependencies;
            public string error;
        }

        #endregion

        public static RpcResponse SearchAssets(RpcRequest request)
        {
            var req = request.GetParams<SearchAssetsRequest>();

            // Build search filter
            var searchFilter = req.filter ?? "";
            
            if (!string.IsNullOrEmpty(req.type))
            {
                searchFilter += $" t:{req.type}";
            }

            if (req.labels != null && req.labels.Length > 0)
            {
                foreach (var label in req.labels)
                {
                    searchFilter += $" l:{label}";
                }
            }

            // Search
            var guids = AssetDatabase.FindAssets(searchFilter.Trim(), new[] { req.folder });
            var totalCount = guids.Length;

            var assets = guids
                .Take(req.maxResults)
                .Select(guid =>
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    var fileInfo = new FileInfo(path);

                    return new AssetInfo
                    {
                        name = Path.GetFileName(path),
                        path = path,
                        type = asset?.GetType().Name ?? "Unknown",
                        guid = guid,
                        sizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                        labels = AssetDatabase.GetLabels(asset).ToArray()
                    };
                })
                .ToList();

            return RpcResponse.Success(new SearchAssetsResponse
            {
                assets = assets,
                totalCount = totalCount
            });
        }

        public static RpcResponse CreateFolder(RpcRequest request)
        {
            var req = request.GetParams<CreateFolderRequest>();

            if (string.IsNullOrEmpty(req.parentFolder) || string.IsNullOrEmpty(req.folderName))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "parentFolder and folderName are required");
            }

            var guid = AssetDatabase.CreateFolder(req.parentFolder, req.folderName);
            
            if (string.IsNullOrEmpty(guid))
            {
                return RpcResponse.Failure("CREATE_FAILED", $"Failed to create folder '{req.folderName}' in '{req.parentFolder}'");
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);

            return RpcResponse.Success(new CreateFolderResponse
            {
                success = true,
                path = path,
                guid = guid
            });
        }

        public static RpcResponse MoveAsset(RpcRequest request)
        {
            var req = request.GetParams<MoveAssetRequest>();

            if (string.IsNullOrEmpty(req.sourcePath) || string.IsNullOrEmpty(req.destinationPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "sourcePath and destinationPath are required");
            }

            var error = AssetDatabase.MoveAsset(req.sourcePath, req.destinationPath);

            if (!string.IsNullOrEmpty(error))
            {
                return RpcResponse.Success(new MoveAssetResponse
                {
                    success = false,
                    error = error
                });
            }

            return RpcResponse.Success(new MoveAssetResponse
            {
                success = true,
                newPath = req.destinationPath
            });
        }

        public static RpcResponse DeleteAsset(RpcRequest request)
        {
            var req = request.GetParams<DeleteAssetRequest>();

            if (string.IsNullOrEmpty(req.path))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "path is required");
            }

            if (!req.confirm)
            {
                return RpcResponse.Failure("CONFIRMATION_REQUIRED", 
                    $"Set confirm=true to delete asset at '{req.path}'");
            }

            var success = AssetDatabase.DeleteAsset(req.path);

            return RpcResponse.Success(new DeleteAssetResponse
            {
                success = success,
                error = success ? null : $"Failed to delete asset at '{req.path}'"
            });
        }

        public static RpcResponse CreatePrefab(RpcRequest request)
        {
            var req = request.GetParams<CreatePrefabRequest>();

            if (string.IsNullOrEmpty(req.gameObjectPath) || string.IsNullOrEmpty(req.savePath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "gameObjectPath and savePath are required");
            }

            var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
            if (go == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"GameObject not found at path: {req.gameObjectPath}");
            }

            // Ensure path ends with .prefab
            var savePath = req.savePath;
            if (!savePath.EndsWith(".prefab"))
            {
                savePath += ".prefab";
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                // Create directory structure
                var parts = directory.Split('/');
                var currentPath = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var nextPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(nextPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                    }
                    currentPath = nextPath;
                }
            }

            // Check if prefab exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(savePath) != null && !req.overwrite)
            {
                return RpcResponse.Failure("PREFAB_EXISTS", 
                    $"Prefab already exists at '{savePath}'. Set overwrite=true to replace.");
            }

            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
                var guid = AssetDatabase.AssetPathToGUID(savePath);

                return RpcResponse.Success(new CreatePrefabResponse
                {
                    success = true,
                    prefabPath = savePath,
                    guid = guid
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Success(new CreatePrefabResponse
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        public static RpcResponse InstantiatePrefab(RpcRequest request)
        {
            var req = request.GetParams<InstantiatePrefabRequest>();

            if (string.IsNullOrEmpty(req.prefabPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "prefabPath is required");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(req.prefabPath);
            if (prefab == null)
            {
                return RpcResponse.Failure("PREFAB_NOT_FOUND", $"Prefab not found at path: {req.prefabPath}");
            }

            Transform parent = null;
            if (!string.IsNullOrEmpty(req.parentPath))
            {
                var parentGo = GameObjectHandlers.FindGameObjectByPath(req.parentPath);
                if (parentGo == null)
                {
                    return RpcResponse.Failure("PARENT_NOT_FOUND", $"Parent not found at path: {req.parentPath}");
                }
                parent = parentGo.transform;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");

            if (req.position != null)
            {
                instance.transform.localPosition = req.position.ToVector3();
            }
            if (req.rotation != null)
            {
                instance.transform.localEulerAngles = req.rotation.ToVector3();
            }

            return RpcResponse.Success(new InstantiatePrefabResponse
            {
                success = true,
                instancePath = GameObjectHandlers.GetGameObjectPath(instance),
                instanceId = $"go_{instance.GetInstanceID()}"
            });
        }

        public static RpcResponse GetAssetInfo(RpcRequest request)
        {
            var req = request.GetParams<GetAssetInfoRequest>();

            if (string.IsNullOrEmpty(req.path))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "path is required");
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(req.path);
            if (asset == null)
            {
                return RpcResponse.Failure("ASSET_NOT_FOUND", $"Asset not found at path: {req.path}");
            }

            var guid = AssetDatabase.AssetPathToGUID(req.path);
            var fileInfo = new FileInfo(req.path);
            var importer = AssetImporter.GetAtPath(req.path);

            // Get dependencies
            var dependencies = AssetDatabase.GetDependencies(req.path, false).ToList();

            // Get import settings
            var importSettings = new Dictionary<string, object>();
            if (importer != null)
            {
                importSettings["importerType"] = importer.GetType().Name;
                importSettings["assetBundleName"] = importer.assetBundleName;
                importSettings["assetBundleVariant"] = importer.assetBundleVariant;

                if (importer is TextureImporter texImporter)
                {
                    importSettings["textureType"] = texImporter.textureType.ToString();
                    importSettings["maxTextureSize"] = texImporter.maxTextureSize;
                    importSettings["textureCompression"] = texImporter.textureCompression.ToString();
                }
                else if (importer is ModelImporter modelImporter)
                {
                    importSettings["importAnimation"] = modelImporter.importAnimation;
                    importSettings["materialImportMode"] = modelImporter.materialImportMode.ToString();
                    importSettings["globalScale"] = modelImporter.globalScale;
                }
            }

            return RpcResponse.Success(new GetAssetInfoResponse
            {
                success = true,
                asset = new AssetInfo
                {
                    name = asset.name,
                    path = req.path,
                    type = asset.GetType().Name,
                    guid = guid,
                    sizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                    labels = AssetDatabase.GetLabels(asset).ToArray()
                },
                importSettings = importSettings,
                dependencies = dependencies
            });
        }
    }
}
