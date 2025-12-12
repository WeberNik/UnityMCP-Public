// ============================================================================
// UnityVision Bridge - Build Handlers
// Handlers for building Unity players
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class BuildHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class BuildPlayerRequest
        {
            public string targetPlatform; // Android, iOS, StandaloneWindows64, StandaloneOSX
            public string buildPath = "";
            public bool developmentBuild = false;
            public List<string> buildOptions;
            public List<string> scenes;
        }

        [Serializable]
        public class BuildPlayerResponse
        {
            public bool success;
            public string buildPath;
            public float duration;
            public List<string> errors;
            public List<string> warnings;
        }

        #endregion

        public static RpcResponse BuildPlayer(RpcRequest request)
        {
            var req = request.GetParams<BuildPlayerRequest>();

            if (string.IsNullOrEmpty(req.targetPlatform))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "targetPlatform is required");
            }

            try
            {
                var startTime = DateTime.Now;

                // Parse target platform
                BuildTarget target;
                string defaultExtension;
                switch (req.targetPlatform.ToLowerInvariant())
                {
                    case "android":
                        target = BuildTarget.Android;
                        defaultExtension = ".apk";
                        break;
                    case "ios":
                        target = BuildTarget.iOS;
                        defaultExtension = "";
                        break;
                    case "standalonewindows64":
                        target = BuildTarget.StandaloneWindows64;
                        defaultExtension = ".exe";
                        break;
                    case "standaloneosx":
                        target = BuildTarget.StandaloneOSX;
                        defaultExtension = ".app";
                        break;
                    default:
                        return RpcResponse.Failure("INVALID_PLATFORM", $"Unknown platform: {req.targetPlatform}");
                }

                // Determine build path
                string buildPath = req.buildPath;
                if (string.IsNullOrEmpty(buildPath))
                {
                    buildPath = $"Builds/{req.targetPlatform}/{Application.productName}{defaultExtension}";
                }

                // Get scenes
                string[] scenes;
                if (req.scenes != null && req.scenes.Count > 0)
                {
                    scenes = req.scenes.ToArray();
                }
                else
                {
                    scenes = EditorBuildSettings.scenes
                        .Where(s => s.enabled)
                        .Select(s => s.path)
                        .ToArray();
                }

                if (scenes.Length == 0)
                {
                    return RpcResponse.Failure("NO_SCENES", "No scenes to build");
                }

                // Build options
                var options = BuildOptions.None;
                if (req.developmentBuild)
                {
                    options |= BuildOptions.Development;
                }

                if (req.buildOptions != null)
                {
                    foreach (var opt in req.buildOptions)
                    {
                        if (Enum.TryParse<BuildOptions>(opt, true, out var parsed))
                        {
                            options |= parsed;
                        }
                    }
                }

                // Execute build
                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = buildPath,
                    target = target,
                    options = options
                };

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                var duration = (float)(DateTime.Now - startTime).TotalSeconds;

                // Collect errors and warnings
                var errors = new List<string>();
                var warnings = new List<string>();

                foreach (var step in report.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            errors.Add(message.content);
                        }
                        else if (message.type == LogType.Warning)
                        {
                            warnings.Add(message.content);
                        }
                    }
                }

                bool success = report.summary.result == BuildResult.Succeeded;

                return RpcResponse.Success(new BuildPlayerResponse
                {
                    success = success,
                    buildPath = success ? buildPath : null,
                    duration = duration,
                    errors = errors,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("BUILD_ERROR", $"Build failed: {ex.Message}");
            }
        }
    }
}
