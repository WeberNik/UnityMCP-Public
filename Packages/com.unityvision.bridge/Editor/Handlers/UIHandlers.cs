// ============================================================================
// UnityVision Bridge - UI Handlers
// Handlers for UI layout inspection
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class UIHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class DumpUILayoutRequest
        {
            public string rootCanvasPath;
            public int maxDepth = 6;
            public bool includeInactive = true;
        }

        [Serializable]
        public class RectTransformData
        {
            public Vector2Data anchoredPosition;
            public Vector2Data sizeDelta;
            public Vector2Data anchorMin;
            public Vector2Data anchorMax;
            public Vector2Data pivot;
        }

        [Serializable]
        public class UIElementNode
        {
            public string name;
            public string path;
            public bool active;
            public RectTransformData rect;
            public List<string> components;
            public List<UIElementNode> children;
        }

        [Serializable]
        public class DumpUILayoutResponse
        {
            public UIElementNode root;
        }

        #endregion

        public static RpcResponse DumpUILayout(RpcRequest request)
        {
            var req = request.GetParams<DumpUILayoutRequest>();

            if (string.IsNullOrEmpty(req.rootCanvasPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "rootCanvasPath is required");
            }

            var go = GameObjectHandlers.FindGameObjectByPath(req.rootCanvasPath);
            if (go == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"GameObject not found: {req.rootCanvasPath}");
            }

            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return RpcResponse.Failure("NOT_UI_ELEMENT",
                    $"GameObject '{req.rootCanvasPath}' does not have a RectTransform component");
            }

            var root = BuildUIElementNode(go, "", req.maxDepth, 0, req.includeInactive);

            return RpcResponse.Success(new DumpUILayoutResponse { root = root });
        }

        private static UIElementNode BuildUIElementNode(
            GameObject go,
            string parentPath,
            int maxDepth,
            int currentDepth,
            bool includeInactive)
        {
            if (!includeInactive && !go.activeInHierarchy)
            {
                return null;
            }

            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return null;
            }

            string path = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";

            var node = new UIElementNode
            {
                name = go.name,
                path = path,
                active = go.activeSelf,
                rect = new RectTransformData
                {
                    anchoredPosition = Vector2Data.FromVector2(rectTransform.anchoredPosition),
                    sizeDelta = Vector2Data.FromVector2(rectTransform.sizeDelta),
                    anchorMin = Vector2Data.FromVector2(rectTransform.anchorMin),
                    anchorMax = Vector2Data.FromVector2(rectTransform.anchorMax),
                    pivot = Vector2Data.FromVector2(rectTransform.pivot)
                },
                components = go.GetComponents<Component>()
                    .Where(c => c != null && c.GetType() != typeof(RectTransform) && c.GetType() != typeof(CanvasRenderer))
                    .Select(c => c.GetType().FullName)
                    .ToList()
            };

            // Build children
            if (currentDepth < maxDepth)
            {
                node.children = new List<UIElementNode>();
                foreach (Transform child in rectTransform)
                {
                    var childNode = BuildUIElementNode(child.gameObject, path, maxDepth, currentDepth + 1, includeInactive);
                    if (childNode != null)
                    {
                        node.children.Add(childNode);
                    }
                }
            }

            return node;
        }
    }
}
