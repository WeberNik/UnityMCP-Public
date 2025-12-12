// ============================================================================
// UnityVision Bridge - Profiler Handlers
// Handlers for performance profiling and rendering statistics
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class ProfilerHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetRenderingStatsResponse
        {
            public int drawCalls;
            public int batches;
            public int triangles;
            public int vertices;
            public int setPassCalls;
            public int shadowCasters;
            public int visibleSkinnedMeshes;
            public RenderingInfo rendering;
            public MemoryStats memory;
        }

        [Serializable]
        public class RenderingInfo
        {
            public int screenWidth;
            public int screenHeight;
            public float currentFps;
            public float targetFrameRate;
            public string colorSpace;
            public string renderPipeline;
            public int qualityLevel;
            public string qualityName;
        }

        [Serializable]
        public class MemoryStats
        {
            public long totalAllocatedMemory;
            public long totalReservedMemory;
            public long totalUnusedReservedMemory;
            public long monoHeapSize;
            public long monoUsedSize;
            public long graphicsMemory;
            public string totalAllocatedFormatted;
            public string graphicsMemoryFormatted;
        }

        [Serializable]
        public class CaptureProfilerRequest
        {
            public int frameCount = 10;
            public bool includeDeepProfile = false;
        }

        [Serializable]
        public class CaptureProfilerResponse
        {
            public int framesCaptured;
            public float averageFps;
            public float minFps;
            public float maxFps;
            public List<FrameData> frames;
            public List<HotspotInfo> cpuHotspots;
            public List<string> warnings;
        }

        [Serializable]
        public class FrameData
        {
            public int frameIndex;
            public float frameTimeMs;
            public float fps;
        }

        [Serializable]
        public class HotspotInfo
        {
            public string name;
            public float totalTimeMs;
            public float selfTimeMs;
            public int callCount;
            public float percentOfFrame;
        }

        [Serializable]
        public class GetMemorySnapshotResponse
        {
            public long totalMemory;
            public string totalMemoryFormatted;
            public List<MemoryCategory> categories;
            public List<LargeAllocation> largestAllocations;
        }

        [Serializable]
        public class MemoryCategory
        {
            public string name;
            public long bytes;
            public string formatted;
            public float percentOfTotal;
        }

        [Serializable]
        public class LargeAllocation
        {
            public string name;
            public string type;
            public long bytes;
            public string formatted;
        }

        [Serializable]
        public class PerformanceRecommendationsResponse
        {
            public List<PerformanceIssue> issues;
            public List<string> recommendations;
            public string overallHealth;  // "Good", "Warning", "Critical"
        }

        [Serializable]
        public class PerformanceIssue
        {
            public string category;
            public string severity;  // "Info", "Warning", "Error"
            public string description;
            public string recommendation;
        }

        #endregion

        public static RpcResponse GetRenderingStats(RpcRequest request)
        {
            try
            {
                var response = new GetRenderingStatsResponse
                {
                    rendering = new RenderingInfo(),
                    memory = new MemoryStats()
                };

                // Note: Some stats only available in Play mode or with profiler
                // We'll get what we can from the editor

                // Screen info
                response.rendering.screenWidth = Screen.width;
                response.rendering.screenHeight = Screen.height;
                response.rendering.targetFrameRate = Application.targetFrameRate;
                response.rendering.colorSpace = QualitySettings.activeColorSpace.ToString();
                response.rendering.qualityLevel = QualitySettings.GetQualityLevel();
                response.rendering.qualityName = QualitySettings.names[response.rendering.qualityLevel];

                // Render pipeline
                var rpAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                response.rendering.renderPipeline = rpAsset != null ? rpAsset.GetType().Name : "Built-in";

                // Memory stats
                response.memory.totalAllocatedMemory = Profiler.GetTotalAllocatedMemoryLong();
                response.memory.totalReservedMemory = Profiler.GetTotalReservedMemoryLong();
                response.memory.totalUnusedReservedMemory = Profiler.GetTotalUnusedReservedMemoryLong();
                response.memory.monoHeapSize = Profiler.GetMonoHeapSizeLong();
                response.memory.monoUsedSize = Profiler.GetMonoUsedSizeLong();
                response.memory.graphicsMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();

                response.memory.totalAllocatedFormatted = FormatBytes(response.memory.totalAllocatedMemory);
                response.memory.graphicsMemoryFormatted = FormatBytes(response.memory.graphicsMemory);

                // Count scene objects for estimates
                var allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
                var allMeshFilters = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true);
                var allSkinnedMeshes = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>(true);
                var allLights = UnityEngine.Object.FindObjectsOfType<Light>(true);

                // Estimate triangles and vertices
                long totalTris = 0;
                long totalVerts = 0;
                foreach (var mf in allMeshFilters)
                {
                    if (mf.sharedMesh != null)
                    {
                        totalTris += mf.sharedMesh.triangles.Length / 3;
                        totalVerts += mf.sharedMesh.vertexCount;
                    }
                }
                foreach (var smr in allSkinnedMeshes)
                {
                    if (smr.sharedMesh != null)
                    {
                        totalTris += smr.sharedMesh.triangles.Length / 3;
                        totalVerts += smr.sharedMesh.vertexCount;
                    }
                }

                response.triangles = (int)totalTris;
                response.vertices = (int)totalVerts;
                response.visibleSkinnedMeshes = allSkinnedMeshes.Count(s => s.enabled && s.gameObject.activeInHierarchy);

                // Count shadow casters
                response.shadowCasters = allLights.Count(l => l.shadows != LightShadows.None && l.enabled);

                // Estimate draw calls (very rough - actual count requires profiler)
                response.drawCalls = allRenderers.Count(r => r.enabled && r.gameObject.activeInHierarchy);
                response.batches = response.drawCalls; // Without profiler, assume 1:1

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PROFILER_ERROR", ex.Message);
            }
        }

        public static RpcResponse GetMemorySnapshot(RpcRequest request)
        {
            try
            {
                var response = new GetMemorySnapshotResponse
                {
                    categories = new List<MemoryCategory>(),
                    largestAllocations = new List<LargeAllocation>()
                };

                response.totalMemory = Profiler.GetTotalAllocatedMemoryLong();
                response.totalMemoryFormatted = FormatBytes(response.totalMemory);

                // Memory categories
                long graphicsMem = Profiler.GetAllocatedMemoryForGraphicsDriver();
                long monoMem = Profiler.GetMonoUsedSizeLong();
                long nativeMem = response.totalMemory - monoMem;

                response.categories.Add(new MemoryCategory
                {
                    name = "Graphics/Textures",
                    bytes = graphicsMem,
                    formatted = FormatBytes(graphicsMem),
                    percentOfTotal = response.totalMemory > 0 ? (float)graphicsMem / response.totalMemory * 100 : 0
                });

                response.categories.Add(new MemoryCategory
                {
                    name = "Managed (Mono)",
                    bytes = monoMem,
                    formatted = FormatBytes(monoMem),
                    percentOfTotal = response.totalMemory > 0 ? (float)monoMem / response.totalMemory * 100 : 0
                });

                response.categories.Add(new MemoryCategory
                {
                    name = "Native",
                    bytes = nativeMem,
                    formatted = FormatBytes(nativeMem),
                    percentOfTotal = response.totalMemory > 0 ? (float)nativeMem / response.totalMemory * 100 : 0
                });

                // Find large textures
                var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
                var textureList = textures
                    .Where(t => t != null && !string.IsNullOrEmpty(t.name))
                    .Select(t => new { tex = t, size = Profiler.GetRuntimeMemorySizeLong(t) })
                    .OrderByDescending(x => x.size)
                    .Take(10);

                foreach (var item in textureList)
                {
                    response.largestAllocations.Add(new LargeAllocation
                    {
                        name = item.tex.name,
                        type = "Texture2D",
                        bytes = item.size,
                        formatted = FormatBytes(item.size)
                    });
                }

                // Find large meshes
                var meshes = Resources.FindObjectsOfTypeAll<Mesh>();
                var meshList = meshes
                    .Where(m => m != null && !string.IsNullOrEmpty(m.name))
                    .Select(m => new { mesh = m, size = Profiler.GetRuntimeMemorySizeLong(m) })
                    .OrderByDescending(x => x.size)
                    .Take(5);

                foreach (var item in meshList)
                {
                    response.largestAllocations.Add(new LargeAllocation
                    {
                        name = item.mesh.name,
                        type = "Mesh",
                        bytes = item.size,
                        formatted = FormatBytes(item.size)
                    });
                }

                // Sort all by size
                response.largestAllocations = response.largestAllocations
                    .OrderByDescending(a => a.bytes)
                    .Take(15)
                    .ToList();

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PROFILER_ERROR", ex.Message);
            }
        }

        public static RpcResponse GetPerformanceRecommendations(RpcRequest request)
        {
            try
            {
                var response = new PerformanceRecommendationsResponse
                {
                    issues = new List<PerformanceIssue>(),
                    recommendations = new List<string>()
                };

                // Analyze scene
                var allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
                var allMeshFilters = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true);
                var allLights = UnityEngine.Object.FindObjectsOfType<Light>(true);
                var allCameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
                var allCanvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);

                int issueCount = 0;

                // Check draw calls
                int activeRenderers = allRenderers.Count(r => r.enabled && r.gameObject.activeInHierarchy);
                if (activeRenderers > 500)
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "Rendering",
                        severity = activeRenderers > 1000 ? "Error" : "Warning",
                        description = $"High number of renderers: {activeRenderers}",
                        recommendation = "Consider using GPU instancing, static batching, or LOD groups"
                    });
                    issueCount++;
                }

                // Check realtime lights
                int realtimeLights = allLights.Count(l => l.type != LightType.Rectangle && l.lightmapBakeType == LightmapBakeType.Realtime);
                if (realtimeLights > 4)
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "Lighting",
                        severity = realtimeLights > 8 ? "Error" : "Warning",
                        description = $"Many realtime lights: {realtimeLights}",
                        recommendation = "Bake lighting where possible, use light probes for dynamic objects"
                    });
                    issueCount++;
                }

                // Check shadow casters
                int shadowLights = allLights.Count(l => l.shadows != LightShadows.None);
                if (shadowLights > 2)
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "Shadows",
                        severity = "Warning",
                        description = $"Multiple shadow-casting lights: {shadowLights}",
                        recommendation = "Limit shadow-casting lights, use shadow distance culling"
                    });
                    issueCount++;
                }

                // Check cameras
                int activeCameras = allCameras.Count(c => c.enabled && c.gameObject.activeInHierarchy);
                if (activeCameras > 2)
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "Cameras",
                        severity = "Warning",
                        description = $"Multiple active cameras: {activeCameras}",
                        recommendation = "Disable cameras when not needed, use culling masks"
                    });
                    issueCount++;
                }

                // Check UI canvases
                int worldSpaceCanvases = allCanvases.Count(c => c.renderMode == RenderMode.WorldSpace);
                if (worldSpaceCanvases > 5)
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "UI",
                        severity = "Warning",
                        description = $"Many world space canvases: {worldSpaceCanvases}",
                        recommendation = "Combine UI elements, use canvas groups for visibility"
                    });
                    issueCount++;
                }

                // Check mesh complexity
                long totalTris = 0;
                foreach (var mf in allMeshFilters)
                {
                    if (mf.sharedMesh != null)
                        totalTris += mf.sharedMesh.triangles.Length / 3;
                }
                if (totalTris > 1000000)
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "Geometry",
                        severity = totalTris > 2000000 ? "Error" : "Warning",
                        description = $"High triangle count: {totalTris:N0}",
                        recommendation = "Use LOD groups, optimize meshes, enable occlusion culling"
                    });
                    issueCount++;
                }

                // Memory check
                long totalMem = Profiler.GetTotalAllocatedMemoryLong();
                if (totalMem > 1024L * 1024L * 1024L) // > 1GB
                {
                    response.issues.Add(new PerformanceIssue
                    {
                        category = "Memory",
                        severity = totalMem > 2L * 1024L * 1024L * 1024L ? "Error" : "Warning",
                        description = $"High memory usage: {FormatBytes(totalMem)}",
                        recommendation = "Compress textures, use texture streaming, unload unused assets"
                    });
                    issueCount++;
                }

                // Overall health
                if (issueCount == 0)
                {
                    response.overallHealth = "Good";
                    response.recommendations.Add("No major performance issues detected");
                }
                else if (issueCount <= 2)
                {
                    response.overallHealth = "Warning";
                    response.recommendations.Add("Some optimization opportunities identified");
                }
                else
                {
                    response.overallHealth = "Critical";
                    response.recommendations.Add("Multiple performance issues require attention");
                }

                // General recommendations
                response.recommendations.Add("Use the Unity Profiler for detailed frame analysis");
                response.recommendations.Add("Test on target hardware for accurate performance metrics");

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PROFILER_ERROR", ex.Message);
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F2} {sizes[order]}";
        }
    }
}
