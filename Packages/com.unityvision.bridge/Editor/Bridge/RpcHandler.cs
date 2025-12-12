// ============================================================================
// UnityVision Bridge - RPC Handler
// Dispatches incoming RPC requests to appropriate handlers
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityVision.Editor.Handlers;

namespace UnityVision.Editor.Bridge
{
    /// <summary>
    /// Central dispatcher for RPC method calls
    /// </summary>
    public static class RpcHandler
    {
        private static readonly Dictionary<string, Func<RpcRequest, RpcResponse>> _handlers =
            new Dictionary<string, Func<RpcRequest, RpcResponse>>();

        static RpcHandler()
        {
            RegisterHandlers();
        }

        private static void RegisterHandlers()
        {
            // Editor handlers
            Register("get_editor_state", EditorHandlers.GetEditorState);
            Register("set_play_mode", EditorHandlers.SetPlayMode);
            Register("get_active_context", EditorHandlers.GetActiveContext);

            // Console handlers
            Register("get_console_logs", ConsoleHandlers.GetConsoleLogs);
            Register("clear_console_logs", ConsoleHandlers.ClearConsoleLogs);

            // Scene handlers
            Register("list_scenes", SceneHandlers.ListScenes);
            Register("get_scene_hierarchy", SceneHandlers.GetSceneHierarchy);

            // GameObject handlers
            Register("create_game_object", GameObjectHandlers.CreateGameObject);
            Register("modify_game_object", GameObjectHandlers.ModifyGameObject);
            Register("delete_game_object", GameObjectHandlers.DeleteGameObject);

            // Component handlers
            Register("add_component", ComponentHandlers.AddComponent);
            Register("set_component_properties", ComponentHandlers.SetComponentProperties);
            Register("search_component_types", ComponentHandlers.SearchComponentTypes);

            // UI handlers
            Register("dump_ui_layout", UIHandlers.DumpUILayout);

            // Screenshot handlers
            Register("capture_game_view_screenshot", ScreenshotHandlers.CaptureGameViewScreenshot);
            Register("capture_scene_view_screenshot", ScreenshotHandlers.CaptureSceneViewScreenshot);

            // XR handlers
            Register("set_xr_rig_pose", XRHandlers.SetXRRigPose);
            Register("teleport_xr_rig_to_anchor", XRHandlers.TeleportXRRigToAnchor);

            // Test handlers
            Register("run_tests", TestHandlers.RunTests);

            // Build handlers
            Register("build_player", BuildHandlers.BuildPlayer);

            // Code execution handlers
            Register("execute_code", CodeExecutionHandlers.ExecuteCode);
            Register("evaluate_expression", CodeExecutionHandlers.EvaluateExpression);

            // Menu item handlers
            Register("execute_menu_item", MenuItemHandlers.ExecuteMenuItem);
            Register("list_menu_items", MenuItemHandlers.ListMenuItems);

            // Asset handlers
            Register("search_assets", AssetHandlers.SearchAssets);
            Register("create_folder", AssetHandlers.CreateFolder);
            Register("move_asset", AssetHandlers.MoveAsset);
            Register("delete_asset", AssetHandlers.DeleteAsset);
            Register("create_prefab", AssetHandlers.CreatePrefab);
            Register("instantiate_prefab", AssetHandlers.InstantiatePrefab);
            Register("get_asset_info", AssetHandlers.GetAssetInfo);

            // Batch handlers
            Register("batch_execute", BatchHandlers.BatchExecute);

            // === NEW FEATURE HANDLERS ===

            // Selection handlers (Phase 31)
            Register("get_editor_selection", SelectionHandlers.GetEditorSelection);
            Register("set_editor_selection", SelectionHandlers.SetEditorSelection);

            // Inspector handlers (Phase 22)
            Register("get_component_properties", InspectorHandlers.GetComponentProperties);
            Register("set_component_property", InspectorHandlers.SetComponentProperty);
            Register("compare_components", InspectorHandlers.CompareComponents);

            // Material handlers (Phase 23)
            Register("get_material_properties", MaterialHandlers.GetMaterialProperties);
            Register("set_material_property", MaterialHandlers.SetMaterialProperty);
            Register("list_materials", MaterialHandlers.ListMaterials);
            Register("list_shaders", MaterialHandlers.ListShaders);

            // Query handlers (Phase 24)
            Register("find_objects_with_component", QueryHandlers.FindObjectsWithComponent);
            Register("find_missing_references", QueryHandlers.FindMissingReferences);
            Register("analyze_layers", QueryHandlers.AnalyzeLayers);
            Register("find_objects_in_radius", QueryHandlers.FindObjectsInRadius);

            // Prefab handlers (Phase 25)
            Register("get_prefab_overrides", PrefabHandlers.GetPrefabOverrides);
            Register("apply_prefab_overrides", PrefabHandlers.ApplyPrefabOverrides);
            Register("revert_prefab_overrides", PrefabHandlers.RevertPrefabOverrides);
            Register("find_prefab_instances", PrefabHandlers.FindPrefabInstances);

            // Dependency handlers (Phase 26)
            Register("find_asset_references", DependencyHandlers.FindAssetReferences);
            Register("get_asset_dependencies", DependencyHandlers.GetAssetDependencies);
            Register("find_unused_assets", DependencyHandlers.FindUnusedAssets);

            // Profiler handlers (Phase 27)
            Register("get_rendering_stats", ProfilerHandlers.GetRenderingStats);
            Register("get_memory_snapshot", ProfilerHandlers.GetMemorySnapshot);
            Register("get_performance_recommendations", ProfilerHandlers.GetPerformanceRecommendations);

            // Animation handlers (Phase 29)
            Register("get_animator_state", AnimationHandlers.GetAnimatorState);
            Register("set_animator_parameter", AnimationHandlers.SetAnimatorParameter);
            Register("get_animation_clips", AnimationHandlers.GetAnimationClips);
            Register("play_animation", AnimationHandlers.PlayAnimation);
            Register("sample_animation", AnimationHandlers.SampleAnimation);

            // Audio handlers (Phase 30)
            Register("list_audio_sources", AudioHandlers.ListAudioSources);
            Register("get_audio_clip_info", AudioHandlers.GetAudioClipInfo);
            Register("list_audio_clips", AudioHandlers.ListAudioClips);
            Register("preview_audio", AudioHandlers.PreviewAudio);
            Register("set_audio_source", AudioHandlers.SetAudioSource);

            // ShaderGraph handlers (Phase 32)
            Register("get_shadergraph_info", ShaderGraphHandlers.GetShaderGraphInfo);
            Register("list_shadergraphs", ShaderGraphHandlers.ListShaderGraphs);
            Register("create_shadergraph", ShaderGraphHandlers.CreateShaderGraph);
            Register("list_shadergraph_node_types", ShaderGraphHandlers.ListShaderGraphNodeTypes);

            // === PHASE 40-44 HANDLERS (Dec 2025) ===

            // Script handlers (Phase 41)
            Register("unity_script", ScriptHandler.HandleRpc);

            // Package handlers (Phase 42)
            Register("unity_package", PackageHandler.HandleRpc);

            // Scene CRUD handlers (Phase 43)
            Register("scene_create", SceneHandlers.CreateScene);
            Register("scene_load", SceneHandlers.LoadScene);
            Register("scene_save", SceneHandlers.SaveScene);
            Register("scene_delete", SceneHandlers.DeleteScene);

            // Editor recompile/refresh handlers (Phase 44)
            Register("editor_recompile", EditorHandlers.RecompileScripts);
            Register("editor_refresh", EditorHandlers.RefreshAssets);
            
            // Phase 50: Compilation status
            Register("get_compilation_status", EditorHandlers.GetCompilationStatus);
            
            // Phase 50: Reflection-based console access
            Register("get_console_logs_detailed", ConsoleHandlers.GetConsoleLogsFromUnity);
        }

        private static void Register(string method, Func<RpcRequest, RpcResponse> handler)
        {
            _handlers[method] = handler;
        }

        /// <summary>
        /// Handle an incoming RPC request
        /// </summary>
        public static RpcResponse HandleRequest(RpcRequest request)
        {
            if (request == null)
            {
                return RpcResponse.Failure("INVALID_REQUEST", "Request is null");
            }

            if (string.IsNullOrEmpty(request.Method))
            {
                return RpcResponse.Failure("INVALID_REQUEST", "Method is required");
            }

            if (!_handlers.TryGetValue(request.Method, out var handler))
            {
                return RpcResponse.Failure(
                    "METHOD_NOT_FOUND",
                    $"Unknown method: {request.Method}",
                    new { availableMethods = new List<string>(_handlers.Keys) }
                );
            }

            try
            {
                return handler(request);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVision] Error handling {request.Method}: {ex}");
                return RpcResponse.Failure(
                    "INTERNAL_ERROR",
                    $"Error executing {request.Method}: {ex.Message}",
                    new { stackTrace = ex.StackTrace }
                );
            }
        }
    }
}
