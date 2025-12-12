// ============================================================================
// UnityVision Bridge - Dependency Handlers
// Handlers for asset dependency analysis
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
    public static class DependencyHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class FindReferencesRequest
        {
            public string assetPath;
            public bool searchInScenes = true;
            public bool searchInPrefabs = true;
            public bool searchInMaterials = true;
            public int maxResults = 100;
        }

        [Serializable]
        public class FindReferencesResponse
        {
            public string assetPath;
            public string assetType;
            public int totalReferences;
            public List<AssetReference> references;
        }

        [Serializable]
        public class AssetReference
        {
            public string path;
            public string name;
            public string type;
            public string propertyPath;
        }

        [Serializable]
        public class GetDependenciesRequest
        {
            public string assetPath;
            public bool recursive = false;
            public int maxDepth = 3;
        }

        [Serializable]
        public class GetDependenciesResponse
        {
            public string assetPath;
            public int totalDependencies;
            public List<DependencyInfo> dependencies;
        }

        [Serializable]
        public class DependencyInfo
        {
            public string path;
            public string name;
            public string type;
            public int depth;
            public long sizeBytes;
        }

        [Serializable]
        public class FindUnusedAssetsRequest
        {
            public string folder = "Assets";
            public List<string> excludePatterns;
            public List<string> includeExtensions;
            public int maxResults = 200;
        }

        [Serializable]
        public class FindUnusedAssetsResponse
        {
            public int totalScanned;
            public int unusedCount;
            public long totalSizeBytes;
            public List<UnusedAsset> unusedAssets;
        }

        [Serializable]
        public class UnusedAsset
        {
            public string path;
            public string name;
            public string type;
            public long sizeBytes;
        }

        #endregion

        public static RpcResponse FindAssetReferences(RpcRequest request)
        {
            var req = request.GetParams<FindReferencesRequest>();

            try
            {
                if (!File.Exists(req.assetPath) && !AssetDatabase.IsValidFolder(req.assetPath))
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Asset not found: {req.assetPath}");
                }

                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(req.assetPath);
                var response = new FindReferencesResponse
                {
                    assetPath = req.assetPath,
                    assetType = asset != null ? asset.GetType().Name : "Unknown",
                    references = new List<AssetReference>()
                };

                string assetGuid = AssetDatabase.AssetPathToGUID(req.assetPath);

                // Search all assets that might reference this
                var searchFilters = new List<string>();
                if (req.searchInScenes) searchFilters.Add("t:Scene");
                if (req.searchInPrefabs) searchFilters.Add("t:Prefab");
                if (req.searchInMaterials) searchFilters.Add("t:Material");

                foreach (var filter in searchFilters)
                {
                    if (response.references.Count >= req.maxResults) break;

                    var guids = AssetDatabase.FindAssets(filter);
                    foreach (var guid in guids)
                    {
                        if (response.references.Count >= req.maxResults) break;

                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        
                        // Check if this asset depends on our target
                        var dependencies = AssetDatabase.GetDependencies(path, false);
                        if (dependencies.Contains(req.assetPath))
                        {
                            var refAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            response.references.Add(new AssetReference
                            {
                                path = path,
                                name = refAsset != null ? refAsset.name : Path.GetFileNameWithoutExtension(path),
                                type = refAsset != null ? refAsset.GetType().Name : "Unknown"
                            });
                        }
                    }
                }

                // Also search in ScriptableObjects and other assets
                if (response.references.Count < req.maxResults)
                {
                    var allAssets = AssetDatabase.FindAssets("t:ScriptableObject");
                    foreach (var guid in allAssets)
                    {
                        if (response.references.Count >= req.maxResults) break;

                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var dependencies = AssetDatabase.GetDependencies(path, false);
                        if (dependencies.Contains(req.assetPath))
                        {
                            var refAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (!response.references.Any(r => r.path == path))
                            {
                                response.references.Add(new AssetReference
                                {
                                    path = path,
                                    name = refAsset != null ? refAsset.name : Path.GetFileNameWithoutExtension(path),
                                    type = refAsset != null ? refAsset.GetType().Name : "Unknown"
                                });
                            }
                        }
                    }
                }

                response.totalReferences = response.references.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("DEPENDENCY_ERROR", ex.Message);
            }
        }

        public static RpcResponse GetAssetDependencies(RpcRequest request)
        {
            var req = request.GetParams<GetDependenciesRequest>();

            try
            {
                if (!File.Exists(req.assetPath) && !AssetDatabase.IsValidFolder(req.assetPath))
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Asset not found: {req.assetPath}");
                }

                var response = new GetDependenciesResponse
                {
                    assetPath = req.assetPath,
                    dependencies = new List<DependencyInfo>()
                };

                var dependencies = AssetDatabase.GetDependencies(req.assetPath, req.recursive);
                var processed = new HashSet<string> { req.assetPath };

                foreach (var depPath in dependencies)
                {
                    if (depPath == req.assetPath) continue;
                    if (processed.Contains(depPath)) continue;
                    processed.Add(depPath);

                    var depAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(depPath);
                    long size = 0;
                    if (File.Exists(depPath))
                    {
                        size = new FileInfo(depPath).Length;
                    }

                    response.dependencies.Add(new DependencyInfo
                    {
                        path = depPath,
                        name = depAsset != null ? depAsset.name : Path.GetFileNameWithoutExtension(depPath),
                        type = depAsset != null ? depAsset.GetType().Name : "Unknown",
                        depth = 1, // Would need recursive tracking for accurate depth
                        sizeBytes = size
                    });
                }

                response.totalDependencies = response.dependencies.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("DEPENDENCY_ERROR", ex.Message);
            }
        }

        public static RpcResponse FindUnusedAssets(RpcRequest request)
        {
            var req = request.GetParams<FindUnusedAssetsRequest>();

            try
            {
                var response = new FindUnusedAssetsResponse
                {
                    unusedAssets = new List<UnusedAsset>()
                };

                // Get all assets in build
                var usedAssets = new HashSet<string>();

                // Get dependencies from all scenes in build settings
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (!scene.enabled) continue;
                    var deps = AssetDatabase.GetDependencies(scene.path, true);
                    foreach (var dep in deps)
                    {
                        usedAssets.Add(dep);
                    }
                }

                // Also check Resources folders
                var resourceAssets = AssetDatabase.FindAssets("", new[] { "Assets/Resources" });
                foreach (var guid in resourceAssets)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    usedAssets.Add(path);
                    var deps = AssetDatabase.GetDependencies(path, true);
                    foreach (var dep in deps)
                    {
                        usedAssets.Add(dep);
                    }
                }

                // Default extensions to check
                var extensions = req.includeExtensions ?? new List<string> 
                { 
                    ".png", ".jpg", ".jpeg", ".tga", ".psd",
                    ".fbx", ".obj", ".blend",
                    ".mat", ".prefab",
                    ".wav", ".mp3", ".ogg",
                    ".asset"
                };

                // Default exclude patterns
                var excludePatterns = req.excludePatterns ?? new List<string>
                {
                    "Editor/",
                    "Plugins/",
                    "Resources/",
                    "StreamingAssets/"
                };

                // Scan folder
                var allFiles = Directory.GetFiles(req.folder, "*.*", SearchOption.AllDirectories);
                response.totalScanned = allFiles.Length;

                foreach (var file in allFiles)
                {
                    if (response.unusedAssets.Count >= req.maxResults) break;

                    string assetPath = file.Replace("\\", "/");
                    
                    // Skip meta files
                    if (assetPath.EndsWith(".meta")) continue;

                    // Check extension
                    string ext = Path.GetExtension(assetPath).ToLower();
                    if (!extensions.Contains(ext)) continue;

                    // Check exclude patterns
                    bool excluded = false;
                    foreach (var pattern in excludePatterns)
                    {
                        if (assetPath.Contains(pattern))
                        {
                            excluded = true;
                            break;
                        }
                    }
                    if (excluded) continue;

                    // Check if used
                    if (!usedAssets.Contains(assetPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        long size = new FileInfo(file).Length;

                        response.unusedAssets.Add(new UnusedAsset
                        {
                            path = assetPath,
                            name = asset != null ? asset.name : Path.GetFileNameWithoutExtension(assetPath),
                            type = asset != null ? asset.GetType().Name : "Unknown",
                            sizeBytes = size
                        });

                        response.totalSizeBytes += size;
                    }
                }

                response.unusedCount = response.unusedAssets.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("DEPENDENCY_ERROR", ex.Message);
            }
        }
    }
}
