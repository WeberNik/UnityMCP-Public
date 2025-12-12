// ============================================================================
// UnityVision Bridge - Animation Handlers
// Handlers for animation inspection and control
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class AnimationHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetAnimatorStateRequest
        {
            public string gameObjectPath;
        }

        [Serializable]
        public class GetAnimatorStateResponse
        {
            public bool hasAnimator;
            public bool isPlaying;
            public string currentStateName;
            public float normalizedTime;
            public float speed;
            public List<AnimatorParameterInfo> parameters;
            public List<AnimatorLayerInfo> layers;
            public List<string> availableStates;
        }

        [Serializable]
        public class AnimatorParameterInfo
        {
            public string name;
            public string type;  // Float, Int, Bool, Trigger
            public string value;
        }

        [Serializable]
        public class AnimatorLayerInfo
        {
            public int index;
            public string name;
            public float weight;
            public string currentState;
        }

        [Serializable]
        public class SetAnimatorParameterRequest
        {
            public string gameObjectPath;
            public string parameterName;
            public string value;
            public string type;  // Float, Int, Bool, Trigger
        }

        [Serializable]
        public class SetAnimatorParameterResponse
        {
            public bool success;
            public string previousValue;
        }

        [Serializable]
        public class GetAnimationClipsRequest
        {
            public string gameObjectPath;
            public string animatorControllerPath;  // Alternative: load from asset
        }

        [Serializable]
        public class GetAnimationClipsResponse
        {
            public List<AnimationClipInfo> clips;
        }

        [Serializable]
        public class AnimationClipInfo
        {
            public string name;
            public float length;
            public float frameRate;
            public bool isLooping;
            public bool isHumanMotion;
            public int eventCount;
            public List<string> animatedProperties;
        }

        [Serializable]
        public class PlayAnimationRequest
        {
            public string gameObjectPath;
            public string stateName;
            public int layer = 0;
            public float normalizedTime = 0f;
        }

        [Serializable]
        public class PlayAnimationResponse
        {
            public bool success;
            public string message;
        }

        [Serializable]
        public class SampleAnimationRequest
        {
            public string gameObjectPath;
            public string clipName;
            public float time;
            public bool takeScreenshot = false;
        }

        [Serializable]
        public class SampleAnimationResponse
        {
            public bool success;
            public float sampledTime;
            public string screenshotBase64;  // If requested
        }

        #endregion

        public static RpcResponse GetAnimatorState(RpcRequest request)
        {
            var req = request.GetParams<GetAnimatorStateRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                var animator = go.GetComponent<Animator>();
                var response = new GetAnimatorStateResponse
                {
                    hasAnimator = animator != null,
                    parameters = new List<AnimatorParameterInfo>(),
                    layers = new List<AnimatorLayerInfo>(),
                    availableStates = new List<string>()
                };

                if (animator == null)
                {
                    return RpcResponse.Success(response);
                }

                response.isPlaying = Application.isPlaying && animator.enabled;
                response.speed = animator.speed;

                // Get current state info
                if (animator.runtimeAnimatorController != null)
                {
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    response.normalizedTime = stateInfo.normalizedTime;

                    // Get state name from controller
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    if (controller != null)
                    {
                        foreach (var layer in controller.layers)
                        {
                            foreach (var state in layer.stateMachine.states)
                            {
                                response.availableStates.Add(state.state.name);
                                if (stateInfo.IsName(state.state.name))
                                {
                                    response.currentStateName = state.state.name;
                                }
                            }
                        }
                    }
                }

                // Get parameters
                foreach (var param in animator.parameters)
                {
                    var paramInfo = new AnimatorParameterInfo
                    {
                        name = param.name,
                        type = param.type.ToString()
                    };

                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            paramInfo.value = animator.GetFloat(param.name).ToString("F3");
                            break;
                        case AnimatorControllerParameterType.Int:
                            paramInfo.value = animator.GetInteger(param.name).ToString();
                            break;
                        case AnimatorControllerParameterType.Bool:
                            paramInfo.value = animator.GetBool(param.name).ToString();
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            paramInfo.value = "Trigger";
                            break;
                    }

                    response.parameters.Add(paramInfo);
                }

                // Get layer info
                for (int i = 0; i < animator.layerCount; i++)
                {
                    var layerInfo = new AnimatorLayerInfo
                    {
                        index = i,
                        name = animator.GetLayerName(i),
                        weight = animator.GetLayerWeight(i)
                    };

                    if (Application.isPlaying)
                    {
                        var stateInfo = animator.GetCurrentAnimatorStateInfo(i);
                        // Try to find state name
                        var controller = animator.runtimeAnimatorController as AnimatorController;
                        if (controller != null && i < controller.layers.Length)
                        {
                            foreach (var state in controller.layers[i].stateMachine.states)
                            {
                                if (stateInfo.IsName(state.state.name))
                                {
                                    layerInfo.currentState = state.state.name;
                                    break;
                                }
                            }
                        }
                    }

                    response.layers.Add(layerInfo);
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("ANIMATION_ERROR", ex.Message);
            }
        }

        public static RpcResponse SetAnimatorParameter(RpcRequest request)
        {
            var req = request.GetParams<SetAnimatorParameterRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                var animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    return RpcResponse.Failure("NO_ANIMATOR", "GameObject has no Animator component");
                }

                string previousValue = "";

                // Find parameter type if not specified
                AnimatorControllerParameterType paramType = AnimatorControllerParameterType.Float;
                bool found = false;
                foreach (var param in animator.parameters)
                {
                    if (param.name == req.parameterName)
                    {
                        paramType = param.type;
                        found = true;

                        // Get previous value
                        switch (paramType)
                        {
                            case AnimatorControllerParameterType.Float:
                                previousValue = animator.GetFloat(param.name).ToString();
                                break;
                            case AnimatorControllerParameterType.Int:
                                previousValue = animator.GetInteger(param.name).ToString();
                                break;
                            case AnimatorControllerParameterType.Bool:
                                previousValue = animator.GetBool(param.name).ToString();
                                break;
                        }
                        break;
                    }
                }

                if (!found)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Parameter not found: {req.parameterName}");
                }

                // Override type if specified
                if (!string.IsNullOrEmpty(req.type))
                {
                    Enum.TryParse(req.type, true, out paramType);
                }

                // Set value
                switch (paramType)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(req.parameterName, float.Parse(req.value));
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(req.parameterName, int.Parse(req.value));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(req.parameterName, bool.Parse(req.value));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        animator.SetTrigger(req.parameterName);
                        break;
                }

                return RpcResponse.Success(new SetAnimatorParameterResponse
                {
                    success = true,
                    previousValue = previousValue
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("ANIMATION_ERROR", ex.Message);
            }
        }

        public static RpcResponse GetAnimationClips(RpcRequest request)
        {
            var req = request.GetParams<GetAnimationClipsRequest>();

            try
            {
                var response = new GetAnimationClipsResponse
                {
                    clips = new List<AnimationClipInfo>()
                };

                AnimationClip[] clips = null;

                // Get from GameObject's Animator
                if (!string.IsNullOrEmpty(req.gameObjectPath))
                {
                    var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                    if (go != null)
                    {
                        var animator = go.GetComponent<Animator>();
                        if (animator != null && animator.runtimeAnimatorController != null)
                        {
                            clips = animator.runtimeAnimatorController.animationClips;
                        }

                        // Also check Animation component
                        var animation = go.GetComponent<Animation>();
                        if (animation != null)
                        {
                            var clipList = new List<AnimationClip>();
                            foreach (AnimationState state in animation)
                            {
                                if (state.clip != null)
                                    clipList.Add(state.clip);
                            }
                            clips = clipList.ToArray();
                        }
                    }
                }
                // Get from AnimatorController asset
                else if (!string.IsNullOrEmpty(req.animatorControllerPath))
                {
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(req.animatorControllerPath);
                    if (controller != null)
                    {
                        clips = controller.animationClips;
                    }
                }

                if (clips != null)
                {
                    foreach (var clip in clips)
                    {
                        if (clip == null) continue;

                        var clipInfo = new AnimationClipInfo
                        {
                            name = clip.name,
                            length = clip.length,
                            frameRate = clip.frameRate,
                            isLooping = clip.isLooping,
                            isHumanMotion = clip.isHumanMotion,
                            eventCount = clip.events.Length,
                            animatedProperties = new List<string>()
                        };

                        // Get animated properties
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        foreach (var binding in bindings.Take(20)) // Limit to avoid huge responses
                        {
                            clipInfo.animatedProperties.Add($"{binding.path}/{binding.propertyName}");
                        }

                        response.clips.Add(clipInfo);
                    }
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("ANIMATION_ERROR", ex.Message);
            }
        }

        public static RpcResponse PlayAnimation(RpcRequest request)
        {
            var req = request.GetParams<PlayAnimationRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                var animator = go.GetComponent<Animator>();
                if (animator == null)
                {
                    return RpcResponse.Failure("NO_ANIMATOR", "GameObject has no Animator component");
                }

                if (!Application.isPlaying)
                {
                    return RpcResponse.Failure("NOT_PLAYING", "Animation playback requires Play mode");
                }

                animator.Play(req.stateName, req.layer, req.normalizedTime);

                return RpcResponse.Success(new PlayAnimationResponse
                {
                    success = true,
                    message = $"Playing {req.stateName}"
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("ANIMATION_ERROR", ex.Message);
            }
        }

        public static RpcResponse SampleAnimation(RpcRequest request)
        {
            var req = request.GetParams<SampleAnimationRequest>();

            try
            {
                var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                }

                // Find the clip
                AnimationClip clip = null;
                var animator = go.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    clip = animator.runtimeAnimatorController.animationClips
                        .FirstOrDefault(c => c.name == req.clipName);
                }

                if (clip == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Animation clip not found: {req.clipName}");
                }

                // Sample the animation
                float sampleTime = Mathf.Clamp(req.time, 0, clip.length);
                clip.SampleAnimation(go, sampleTime);

                var response = new SampleAnimationResponse
                {
                    success = true,
                    sampledTime = sampleTime
                };

                // Take screenshot if requested
                if (req.takeScreenshot)
                {
                    // Force scene view repaint
                    SceneView.RepaintAll();
                    
                    // Use existing screenshot functionality
                    var screenshotRequest = new RpcRequest();
                    // Note: Would need to call ScreenshotHandlers here
                    // For now, just indicate screenshot was requested
                    response.screenshotBase64 = "[Screenshot capture would go here]";
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("ANIMATION_ERROR", ex.Message);
            }
        }
    }
}
