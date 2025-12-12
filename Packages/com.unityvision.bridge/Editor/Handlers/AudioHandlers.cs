// ============================================================================
// UnityVision Bridge - Audio Handlers
// Handlers for audio inspection and preview
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class AudioHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class ListAudioSourcesResponse
        {
            public int totalCount;
            public List<AudioSourceInfo> audioSources;
        }

        [Serializable]
        public class AudioSourceInfo
        {
            public string gameObjectPath;
            public string clipName;
            public float volume;
            public float pitch;
            public bool loop;
            public bool playOnAwake;
            public bool isPlaying;
            public bool mute;
            public float spatialBlend;  // 0 = 2D, 1 = 3D
            public float minDistance;
            public float maxDistance;
            public string outputMixer;
        }

        [Serializable]
        public class GetAudioClipInfoRequest
        {
            public string assetPath;
        }

        [Serializable]
        public class GetAudioClipInfoResponse
        {
            public string name;
            public string assetPath;
            public float length;
            public int channels;
            public int frequency;
            public int samples;
            public bool ambisonic;
            public string loadType;
            public string compressionFormat;
            public long fileSizeBytes;
            public string fileSizeFormatted;
            public bool preloadAudioData;
            public bool loadInBackground;
        }

        [Serializable]
        public class ListAudioClipsRequest
        {
            public string folder = "Assets";
            public string nameFilter;
            public int maxResults = 100;
        }

        [Serializable]
        public class ListAudioClipsResponse
        {
            public int totalCount;
            public List<AudioClipSummary> clips;
        }

        [Serializable]
        public class AudioClipSummary
        {
            public string path;
            public string name;
            public float length;
            public int channels;
            public long sizeBytes;
        }

        [Serializable]
        public class PreviewAudioRequest
        {
            public string assetPath;
            public string action;  // "play", "stop", "pause"
        }

        [Serializable]
        public class PreviewAudioResponse
        {
            public bool success;
            public string message;
        }

        [Serializable]
        public class SetAudioSourceRequest
        {
            public string gameObjectPath;
            public float? volume;
            public float? pitch;
            public bool? loop;
            public bool? mute;
            public string action;  // "play", "stop", "pause"
        }

        [Serializable]
        public class SetAudioSourceResponse
        {
            public bool success;
            public string message;
        }

        #endregion

        public static RpcResponse ListAudioSources(RpcRequest request)
        {
            try
            {
                var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>(true);
                var response = new ListAudioSourcesResponse
                {
                    totalCount = audioSources.Length,
                    audioSources = new List<AudioSourceInfo>()
                };

                foreach (var source in audioSources)
                {
                    response.audioSources.Add(new AudioSourceInfo
                    {
                        gameObjectPath = GetGameObjectPath(source.gameObject),
                        clipName = source.clip != null ? source.clip.name : "None",
                        volume = source.volume,
                        pitch = source.pitch,
                        loop = source.loop,
                        playOnAwake = source.playOnAwake,
                        isPlaying = source.isPlaying,
                        mute = source.mute,
                        spatialBlend = source.spatialBlend,
                        minDistance = source.minDistance,
                        maxDistance = source.maxDistance,
                        outputMixer = source.outputAudioMixerGroup != null ? source.outputAudioMixerGroup.name : "None"
                    });
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("AUDIO_ERROR", ex.Message);
            }
        }

        public static RpcResponse GetAudioClipInfo(RpcRequest request)
        {
            var req = request.GetParams<GetAudioClipInfoRequest>();

            try
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(req.assetPath);
                if (clip == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Audio clip not found: {req.assetPath}");
                }

                // Get import settings
                var importer = AssetImporter.GetAtPath(req.assetPath) as AudioImporter;
                
                long fileSize = 0;
                if (System.IO.File.Exists(req.assetPath))
                {
                    fileSize = new System.IO.FileInfo(req.assetPath).Length;
                }

                var response = new GetAudioClipInfoResponse
                {
                    name = clip.name,
                    assetPath = req.assetPath,
                    length = clip.length,
                    channels = clip.channels,
                    frequency = clip.frequency,
                    samples = clip.samples,
                    ambisonic = clip.ambisonic,
                    loadType = clip.loadType.ToString(),
                    preloadAudioData = clip.preloadAudioData,
                    loadInBackground = clip.loadInBackground,
                    fileSizeBytes = fileSize,
                    fileSizeFormatted = FormatBytes(fileSize)
                };

                if (importer != null)
                {
                    var settings = importer.defaultSampleSettings;
                    response.compressionFormat = settings.compressionFormat.ToString();
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("AUDIO_ERROR", ex.Message);
            }
        }

        public static RpcResponse ListAudioClips(RpcRequest request)
        {
            var req = request.GetParams<ListAudioClipsRequest>();

            try
            {
                var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { req.folder });
                var response = new ListAudioClipsResponse
                {
                    totalCount = guids.Length,
                    clips = new List<AudioClipSummary>()
                };

                foreach (var guid in guids)
                {
                    if (response.clips.Count >= req.maxResults) break;

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    
                    if (clip == null) continue;

                    // Apply name filter
                    if (!string.IsNullOrEmpty(req.nameFilter) &&
                        !clip.name.Contains(req.nameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    long size = 0;
                    if (System.IO.File.Exists(path))
                    {
                        size = new System.IO.FileInfo(path).Length;
                    }

                    response.clips.Add(new AudioClipSummary
                    {
                        path = path,
                        name = clip.name,
                        length = clip.length,
                        channels = clip.channels,
                        sizeBytes = size
                    });
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("AUDIO_ERROR", ex.Message);
            }
        }

        public static RpcResponse PreviewAudio(RpcRequest request)
        {
            var req = request.GetParams<PreviewAudioRequest>();

            try
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(req.assetPath);
                if (clip == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Audio clip not found: {req.assetPath}");
                }

                // Use reflection to access the internal AudioUtil class
                var audioUtilType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
                if (audioUtilType == null)
                {
                    return RpcResponse.Failure("NOT_AVAILABLE", "Audio preview not available in this Unity version");
                }

                switch (req.action?.ToLower())
                {
                    case "play":
                        var playMethod = audioUtilType.GetMethod("PlayPreviewClip", 
                            BindingFlags.Static | BindingFlags.Public,
                            null,
                            new Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                            null);
                        
                        if (playMethod != null)
                        {
                            playMethod.Invoke(null, new object[] { clip, 0, false });
                        }
                        else
                        {
                            // Try alternative signature
                            var altPlayMethod = audioUtilType.GetMethod("PlayPreviewClip",
                                BindingFlags.Static | BindingFlags.Public);
                            if (altPlayMethod != null)
                            {
                                altPlayMethod.Invoke(null, new object[] { clip });
                            }
                        }
                        break;

                    case "stop":
                        var stopMethod = audioUtilType.GetMethod("StopAllPreviewClips",
                            BindingFlags.Static | BindingFlags.Public);
                        stopMethod?.Invoke(null, null);
                        break;

                    case "pause":
                        var pauseMethod = audioUtilType.GetMethod("PausePreviewClip",
                            BindingFlags.Static | BindingFlags.Public);
                        pauseMethod?.Invoke(null, null);
                        break;

                    default:
                        return RpcResponse.Failure("INVALID_ACTION", $"Unknown action: {req.action}");
                }

                return RpcResponse.Success(new PreviewAudioResponse
                {
                    success = true,
                    message = $"Audio {req.action}: {clip.name}"
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("AUDIO_ERROR", ex.Message);
            }
        }

        public static RpcResponse SetAudioSource(RpcRequest request)
        {
            var req = request.GetParams<SetAudioSourceRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                var audioSource = go.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    return RpcResponse.Failure("NO_AUDIO_SOURCE", "GameObject has no AudioSource component");
                }

                // Apply property changes
                if (req.volume.HasValue)
                {
                    Undo.RecordObject(audioSource, "Set Volume");
                    audioSource.volume = req.volume.Value;
                }

                if (req.pitch.HasValue)
                {
                    Undo.RecordObject(audioSource, "Set Pitch");
                    audioSource.pitch = req.pitch.Value;
                }

                if (req.loop.HasValue)
                {
                    Undo.RecordObject(audioSource, "Set Loop");
                    audioSource.loop = req.loop.Value;
                }

                if (req.mute.HasValue)
                {
                    Undo.RecordObject(audioSource, "Set Mute");
                    audioSource.mute = req.mute.Value;
                }

                // Handle playback actions (only in play mode)
                string message = "Properties updated";
                if (!string.IsNullOrEmpty(req.action))
                {
                    if (!Application.isPlaying)
                    {
                        message += " (playback requires Play mode)";
                    }
                    else
                    {
                        switch (req.action.ToLower())
                        {
                            case "play":
                                audioSource.Play();
                                message = "Playing audio";
                                break;
                            case "stop":
                                audioSource.Stop();
                                message = "Stopped audio";
                                break;
                            case "pause":
                                audioSource.Pause();
                                message = "Paused audio";
                                break;
                        }
                    }
                }

                EditorUtility.SetDirty(audioSource);

                return RpcResponse.Success(new SetAudioSourceResponse
                {
                    success = true,
                    message = message
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("AUDIO_ERROR", ex.Message);
            }
        }

        #region Helper Methods

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

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F2} {sizes[order]}";
        }

        #endregion
    }
}
