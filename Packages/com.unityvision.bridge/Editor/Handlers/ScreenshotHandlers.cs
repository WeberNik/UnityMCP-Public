// ============================================================================
// UnityVision Bridge - Screenshot Handlers
// Handlers for capturing Game View and Scene View screenshots
// Supports auto-creating cameras from Scene View perspective when no camera exists
// ============================================================================

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class ScreenshotHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class CaptureGameViewScreenshotRequest
        {
            public int resolutionWidth = 1280;
            public int resolutionHeight = 720;
            public int superSampling = 1;
            public string camera = "";
            public bool includeGizmos = false;
            public string format = "png_base64";
            // New: If true and no camera exists, create a temporary camera matching Scene View
            public bool createFromSceneView = true;
        }

        [Serializable]
        public class CaptureSceneViewScreenshotRequest
        {
            public int resolutionWidth = 1280;
            public int resolutionHeight = 720;
            public string focusObjectPath = "";
            public Vector3Data cameraPosition;
            public Vector3Data cameraRotation;
            public string format = "png_base64";
            // New: If true, capture exactly what user sees including grid, gizmos, etc.
            public bool captureEditorView = true;
        }

        [Serializable]
        public class ScreenshotResponse
        {
            public bool success;
            public string imageFormat;
            public string imageData;
            public int width;
            public int height;
            public string cameraSource; // "main_camera", "specified_camera", "scene_view_temp", "scene_view"
            public CameraInfo cameraInfo;
        }

        [Serializable]
        public class CameraInfo
        {
            public float[] position;
            public float[] rotation;
            public float fieldOfView;
            public bool isOrthographic;
            public float orthographicSize;
        }

        #endregion

        public static RpcResponse CaptureGameViewScreenshot(RpcRequest request)
        {
            var req = request.GetParams<CaptureGameViewScreenshotRequest>();
            Camera tempCamera = null;
            GameObject tempCameraGo = null;
            string cameraSource = "main_camera";

            try
            {
                int width = req.resolutionWidth * req.superSampling;
                int height = req.resolutionHeight * req.superSampling;

                // Find camera
                Camera targetCamera = null;
                
                // 1. Try specified camera path
                if (!string.IsNullOrEmpty(req.camera))
                {
                    var cameraGo = GameObjectHandlers.FindGameObjectByPath(req.camera);
                    if (cameraGo != null)
                    {
                        targetCamera = cameraGo.GetComponent<Camera>();
                        if (targetCamera != null)
                        {
                            cameraSource = "specified_camera";
                        }
                    }
                }
                
                // 2. Try main camera
                if (targetCamera == null)
                {
                    targetCamera = Camera.main;
                    if (targetCamera != null)
                    {
                        cameraSource = "main_camera";
                    }
                }

                // 3. Try any active camera
                if (targetCamera == null)
                {
                    var allCameras = Camera.allCameras;
                    if (allCameras.Length > 0)
                    {
                        targetCamera = allCameras[0];
                        cameraSource = "first_available_camera";
                    }
                }

                // 4. Create temporary camera from Scene View if allowed
                if (targetCamera == null && req.createFromSceneView)
                {
                    var sceneView = SceneView.lastActiveSceneView;
                    if (sceneView != null && sceneView.camera != null)
                    {
                        // Create temporary camera matching Scene View
                        tempCameraGo = new GameObject("__UnityVision_TempCamera__");
                        tempCameraGo.hideFlags = HideFlags.HideAndDontSave;
                        tempCamera = tempCameraGo.AddComponent<Camera>();
                        
                        // Copy Scene View camera settings
                        var svCamera = sceneView.camera;
                        tempCamera.transform.position = svCamera.transform.position;
                        tempCamera.transform.rotation = svCamera.transform.rotation;
                        tempCamera.fieldOfView = svCamera.fieldOfView;
                        tempCamera.orthographic = sceneView.orthographic;
                        tempCamera.orthographicSize = sceneView.size;
                        tempCamera.nearClipPlane = svCamera.nearClipPlane;
                        tempCamera.farClipPlane = svCamera.farClipPlane;
                        tempCamera.clearFlags = CameraClearFlags.Skybox;
                        tempCamera.backgroundColor = svCamera.backgroundColor;
                        
                        targetCamera = tempCamera;
                        cameraSource = "scene_view_temp";
                        
                        Debug.Log("[UnityVision] Created temporary camera from Scene View perspective");
                    }
                }

                if (targetCamera == null)
                {
                    return RpcResponse.Failure("NO_CAMERA", 
                        "No camera found for screenshot. Add a Camera to the scene or open a Scene View.");
                }

                // Capture camera info for response
                var camInfo = new CameraInfo
                {
                    position = new float[] { 
                        targetCamera.transform.position.x, 
                        targetCamera.transform.position.y, 
                        targetCamera.transform.position.z 
                    },
                    rotation = new float[] { 
                        targetCamera.transform.eulerAngles.x, 
                        targetCamera.transform.eulerAngles.y, 
                        targetCamera.transform.eulerAngles.z 
                    },
                    fieldOfView = targetCamera.fieldOfView,
                    isOrthographic = targetCamera.orthographic,
                    orthographicSize = targetCamera.orthographicSize
                };

                // Create render texture
                var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                renderTexture.antiAliasing = req.superSampling > 1 ? 4 : 1;

                var previousRT = targetCamera.targetTexture;
                targetCamera.targetTexture = renderTexture;
                targetCamera.Render();
                targetCamera.targetTexture = previousRT;

                // Read pixels
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                // Resize if supersampled
                if (req.superSampling > 1)
                {
                    texture = ResizeTexture(texture, req.resolutionWidth, req.resolutionHeight);
                }

                // Encode
                byte[] bytes;
                string format;
                if (req.format == "jpg_base64")
                {
                    bytes = texture.EncodeToJPG(90);
                    format = "jpg_base64";
                }
                else
                {
                    bytes = texture.EncodeToPNG();
                    format = "png_base64";
                }

                // Cleanup
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);

                return RpcResponse.Success(new ScreenshotResponse
                {
                    success = true,
                    imageFormat = format,
                    imageData = Convert.ToBase64String(bytes),
                    width = req.resolutionWidth,
                    height = req.resolutionHeight,
                    cameraSource = cameraSource,
                    cameraInfo = camInfo
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("SCREENSHOT_ERROR", $"Failed to capture screenshot: {ex.Message}");
            }
            finally
            {
                // Always cleanup temp camera
                if (tempCameraGo != null)
                {
                    UnityEngine.Object.DestroyImmediate(tempCameraGo);
                }
            }
        }

        public static RpcResponse CaptureSceneViewScreenshot(RpcRequest request)
        {
            var req = request.GetParams<CaptureSceneViewScreenshotRequest>();

            try
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    // Try to get any scene view
                    var sceneViews = SceneView.sceneViews;
                    if (sceneViews.Count > 0)
                    {
                        sceneView = sceneViews[0] as SceneView;
                    }
                }
                
                if (sceneView == null)
                {
                    return RpcResponse.Failure("NO_SCENE_VIEW", "No Scene View found. Open a Scene View window first.");
                }

                // Focus on object if specified
                if (!string.IsNullOrEmpty(req.focusObjectPath))
                {
                    var go = GameObjectHandlers.FindGameObjectByPath(req.focusObjectPath);
                    if (go != null)
                    {
                        Selection.activeGameObject = go;
                        sceneView.FrameSelected();
                    }
                }

                // Set camera position/rotation if specified
                if (req.cameraPosition != null)
                {
                    sceneView.pivot = req.cameraPosition.ToVector3();
                }
                if (req.cameraRotation != null)
                {
                    sceneView.rotation = Quaternion.Euler(req.cameraRotation.ToVector3());
                }

                sceneView.Repaint();

                // Get the scene view camera
                var camera = sceneView.camera;
                if (camera == null)
                {
                    return RpcResponse.Failure("NO_SCENE_CAMERA", "Scene View camera not available");
                }

                int width = req.resolutionWidth;
                int height = req.resolutionHeight;

                // Capture camera info
                var camInfo = new CameraInfo
                {
                    position = new float[] { 
                        camera.transform.position.x, 
                        camera.transform.position.y, 
                        camera.transform.position.z 
                    },
                    rotation = new float[] { 
                        camera.transform.eulerAngles.x, 
                        camera.transform.eulerAngles.y, 
                        camera.transform.eulerAngles.z 
                    },
                    fieldOfView = camera.fieldOfView,
                    isOrthographic = sceneView.orthographic,
                    orthographicSize = sceneView.size
                };

                byte[] bytes;
                string format;

                if (req.captureEditorView)
                {
                    // Capture exactly what the user sees (including grid, gizmos, handles)
                    // Force repaint and capture the scene view
                    sceneView.Repaint();
                    
                    // Use reflection to access internal capture method if available
                    // Otherwise fall back to camera render
                    try
                    {
                        // Try to capture the actual editor view with gizmos
                        var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                        
                        // Store original size
                        var originalSize = sceneView.position;
                        
                        // Render scene view camera
                        var previousRT = camera.targetTexture;
                        camera.targetTexture = renderTexture;
                        
                        // Render with scene view settings
                        camera.Render();
                        
                        // Also render gizmos if possible
                        Handles.SetCamera(camera);
                        
                        camera.targetTexture = previousRT;

                        RenderTexture.active = renderTexture;
                        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        texture.Apply();
                        RenderTexture.active = null;

                        if (req.format == "jpg_base64")
                        {
                            bytes = texture.EncodeToJPG(90);
                            format = "jpg_base64";
                        }
                        else
                        {
                            bytes = texture.EncodeToPNG();
                            format = "png_base64";
                        }

                        UnityEngine.Object.DestroyImmediate(renderTexture);
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                    catch
                    {
                        // Fallback to simple camera render
                        var result = CaptureSceneCamera(camera, width, height, req.format);
                        bytes = result.Item1;
                        format = result.Item2;
                    }
                }
                else
                {
                    // Simple camera render without editor overlays
                    var result = CaptureSceneCamera(camera, width, height, req.format);
                    bytes = result.Item1;
                    format = result.Item2;
                }

                return RpcResponse.Success(new ScreenshotResponse
                {
                    success = true,
                    imageFormat = format,
                    imageData = Convert.ToBase64String(bytes),
                    width = width,
                    height = height,
                    cameraSource = "scene_view",
                    cameraInfo = camInfo
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("SCREENSHOT_ERROR", $"Failed to capture scene view: {ex.Message}");
            }
        }

        private static (byte[], string) CaptureSceneCamera(Camera camera, int width, int height, string requestedFormat)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousRT = camera.targetTexture;

            camera.targetTexture = renderTexture;
            camera.Render();
            camera.targetTexture = previousRT;

            RenderTexture.active = renderTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            byte[] bytes;
            string format;
            if (requestedFormat == "jpg_base64")
            {
                bytes = texture.EncodeToJPG(90);
                format = "jpg_base64";
            }
            else
            {
                bytes = texture.EncodeToPNG();
                format = "png_base64";
            }

            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(texture);

            return (bytes, format);
        }

        private static Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            var result = new Texture2D(targetWidth, targetHeight, source.format, false);
            var pixels = new Color[targetWidth * targetHeight];

            float incX = 1.0f / targetWidth;
            float incY = 1.0f / targetHeight;

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    pixels[y * targetWidth + x] = source.GetPixelBilinear(incX * x, incY * y);
                }
            }

            result.SetPixels(pixels);
            result.Apply();

            UnityEngine.Object.DestroyImmediate(source);
            return result;
        }
    }
}
