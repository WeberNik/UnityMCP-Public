// ============================================================================
// UnityVision Bridge - Query Handlers
// Handlers for advanced scene queries - find objects, missing refs, etc.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class QueryHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class FindObjectsRequest
        {
            public string componentType;
            public string tag;
            public string layer;
            public bool? activeOnly;
            public string nameContains;
            public int maxResults = 100;
        }

        [Serializable]
        public class FindObjectsResponse
        {
            public int totalFound;
            public int returnedCount;
            public List<FoundObject> objects;
        }

        [Serializable]
        public class FoundObject
        {
            public string path;
            public string name;
            public string tag;
            public string layer;
            public bool isActive;
            public List<string> components;
        }

        [Serializable]
        public class FindMissingReferencesResponse
        {
            public int totalIssues;
            public List<MissingReference> missingReferences;
            public List<string> missingScripts;
        }

        [Serializable]
        public class MissingReference
        {
            public string gameObjectPath;
            public string componentType;
            public string propertyName;
            public string propertyPath;
        }

        [Serializable]
        public class AnalyzeLayersResponse
        {
            public List<LayerInfo> layers;
            public List<TagInfo> tags;
            public int totalObjects;
        }

        [Serializable]
        public class LayerInfo
        {
            public int index;
            public string name;
            public int objectCount;
        }

        [Serializable]
        public class TagInfo
        {
            public string name;
            public int objectCount;
        }

        [Serializable]
        public class SpatialQueryRequest
        {
            public float[] center;  // x, y, z
            public float radius;
            public string[] layerMask;
            public int maxResults = 50;
        }

        [Serializable]
        public class SpatialQueryResponse
        {
            public int foundCount;
            public List<SpatialResult> results;
        }

        [Serializable]
        public class SpatialResult
        {
            public string path;
            public string name;
            public float distance;
            public float[] position;
        }

        #endregion

        public static RpcResponse FindObjectsWithComponent(RpcRequest request)
        {
            var req = request.GetParams<FindObjectsRequest>();

            try
            {
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
                var results = new List<FoundObject>();

                foreach (var go in allObjects)
                {
                    if (results.Count >= req.maxResults) break;

                    // Filter by component type
                    if (!string.IsNullOrEmpty(req.componentType))
                    {
                        bool hasComponent = false;
                        foreach (var comp in go.GetComponents<Component>())
                        {
                            if (comp == null) continue;
                            if (comp.GetType().Name.Contains(req.componentType, StringComparison.OrdinalIgnoreCase) ||
                                comp.GetType().FullName.Contains(req.componentType, StringComparison.OrdinalIgnoreCase))
                            {
                                hasComponent = true;
                                break;
                            }
                        }
                        if (!hasComponent) continue;
                    }

                    // Filter by tag
                    if (!string.IsNullOrEmpty(req.tag) && go.tag != req.tag)
                    {
                        continue;
                    }

                    // Filter by layer
                    if (!string.IsNullOrEmpty(req.layer) && LayerMask.LayerToName(go.layer) != req.layer)
                    {
                        continue;
                    }

                    // Filter by active state
                    if (req.activeOnly.HasValue && go.activeInHierarchy != req.activeOnly.Value)
                    {
                        continue;
                    }

                    // Filter by name
                    if (!string.IsNullOrEmpty(req.nameContains) && 
                        !go.name.Contains(req.nameContains, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    results.Add(new FoundObject
                    {
                        path = GetGameObjectPath(go),
                        name = go.name,
                        tag = go.tag,
                        layer = LayerMask.LayerToName(go.layer),
                        isActive = go.activeInHierarchy,
                        components = go.GetComponents<Component>()
                            .Where(c => c != null)
                            .Select(c => c.GetType().Name)
                            .ToList()
                    });
                }

                return RpcResponse.Success(new FindObjectsResponse
                {
                    totalFound = results.Count,
                    returnedCount = results.Count,
                    objects = results
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("QUERY_ERROR", ex.Message);
            }
        }

        public static RpcResponse FindMissingReferences(RpcRequest request)
        {
            try
            {
                var response = new FindMissingReferencesResponse
                {
                    missingReferences = new List<MissingReference>(),
                    missingScripts = new List<string>()
                };

                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);

                foreach (var go in allObjects)
                {
                    string goPath = GetGameObjectPath(go);
                    var components = go.GetComponents<Component>();

                    for (int i = 0; i < components.Length; i++)
                    {
                        var comp = components[i];

                        // Check for missing script
                        if (comp == null)
                        {
                            response.missingScripts.Add($"{goPath} [Component {i}]");
                            continue;
                        }

                        // Check for missing references in serialized properties
                        var so = new SerializedObject(comp);
                        var iterator = so.GetIterator();

                        while (iterator.NextVisible(true))
                        {
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                            {
                                if (iterator.objectReferenceValue == null && 
                                    iterator.objectReferenceInstanceIDValue != 0)
                                {
                                    response.missingReferences.Add(new MissingReference
                                    {
                                        gameObjectPath = goPath,
                                        componentType = comp.GetType().Name,
                                        propertyName = iterator.displayName,
                                        propertyPath = iterator.propertyPath
                                    });
                                }
                            }
                        }
                    }
                }

                response.totalIssues = response.missingReferences.Count + response.missingScripts.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("QUERY_ERROR", ex.Message);
            }
        }

        public static RpcResponse AnalyzeLayers(RpcRequest request)
        {
            try
            {
                var response = new AnalyzeLayersResponse
                {
                    layers = new List<LayerInfo>(),
                    tags = new List<TagInfo>()
                };

                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
                response.totalObjects = allObjects.Length;

                // Count by layer
                var layerCounts = new Dictionary<int, int>();
                var tagCounts = new Dictionary<string, int>();

                foreach (var go in allObjects)
                {
                    // Layer
                    if (!layerCounts.ContainsKey(go.layer))
                        layerCounts[go.layer] = 0;
                    layerCounts[go.layer]++;

                    // Tag
                    if (!tagCounts.ContainsKey(go.tag))
                        tagCounts[go.tag] = 0;
                    tagCounts[go.tag]++;
                }

                // Build layer info
                for (int i = 0; i < 32; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(layerName)) continue;

                    response.layers.Add(new LayerInfo
                    {
                        index = i,
                        name = layerName,
                        objectCount = layerCounts.ContainsKey(i) ? layerCounts[i] : 0
                    });
                }

                // Build tag info
                foreach (var kvp in tagCounts.OrderByDescending(k => k.Value))
                {
                    response.tags.Add(new TagInfo
                    {
                        name = kvp.Key,
                        objectCount = kvp.Value
                    });
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("QUERY_ERROR", ex.Message);
            }
        }

        public static RpcResponse FindObjectsInRadius(RpcRequest request)
        {
            var req = request.GetParams<SpatialQueryRequest>();

            try
            {
                Vector3 center = new Vector3(req.center[0], req.center[1], req.center[2]);
                
                // Build layer mask
                int layerMask = -1; // All layers
                if (req.layerMask != null && req.layerMask.Length > 0)
                {
                    layerMask = 0;
                    foreach (var layerName in req.layerMask)
                    {
                        int layer = LayerMask.NameToLayer(layerName);
                        if (layer >= 0)
                        {
                            layerMask |= (1 << layer);
                        }
                    }
                }

                // Use OverlapSphere for physics objects
                var colliders = Physics.OverlapSphere(center, req.radius, layerMask);
                var results = new List<SpatialResult>();

                // Also check all transforms for non-physics objects
                var allObjects = UnityEngine.Object.FindObjectsOfType<Transform>(true);
                var foundPaths = new HashSet<string>();

                // Add physics results first
                foreach (var col in colliders)
                {
                    if (results.Count >= req.maxResults) break;

                    string path = GetGameObjectPath(col.gameObject);
                    if (foundPaths.Contains(path)) continue;
                    foundPaths.Add(path);

                    float distance = Vector3.Distance(center, col.transform.position);
                    results.Add(new SpatialResult
                    {
                        path = path,
                        name = col.gameObject.name,
                        distance = distance,
                        position = new float[] { col.transform.position.x, col.transform.position.y, col.transform.position.z }
                    });
                }

                // Add non-physics objects within radius
                foreach (var t in allObjects)
                {
                    if (results.Count >= req.maxResults) break;

                    float distance = Vector3.Distance(center, t.position);
                    if (distance > req.radius) continue;

                    string path = GetGameObjectPath(t.gameObject);
                    if (foundPaths.Contains(path)) continue;

                    // Check layer mask
                    if (layerMask != -1 && ((1 << t.gameObject.layer) & layerMask) == 0)
                        continue;

                    foundPaths.Add(path);
                    results.Add(new SpatialResult
                    {
                        path = path,
                        name = t.gameObject.name,
                        distance = distance,
                        position = new float[] { t.position.x, t.position.y, t.position.z }
                    });
                }

                // Sort by distance
                results = results.OrderBy(r => r.distance).Take(req.maxResults).ToList();

                return RpcResponse.Success(new SpatialQueryResponse
                {
                    foundCount = results.Count,
                    results = results
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("QUERY_ERROR", ex.Message);
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
