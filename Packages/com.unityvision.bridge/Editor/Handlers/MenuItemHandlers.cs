// ============================================================================
// UnityVision Bridge - Menu Item Handlers
// Execute Unity menu items and list available menu items
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
    public static class MenuItemHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class ExecuteMenuItemRequest
        {
            public string menuPath;
        }

        [Serializable]
        public class ExecuteMenuItemResponse
        {
            public bool success;
            public string menuPath;
            public string error;
        }

        [Serializable]
        public class ListMenuItemsRequest
        {
            public string filter = "";
            public int maxResults = 200;
        }

        [Serializable]
        public class MenuItemInfo
        {
            public string path;
            public bool hasShortcut;
            public string shortcut;
            public int priority;
        }

        [Serializable]
        public class ListMenuItemsResponse
        {
            public List<MenuItemInfo> items;
            public int totalCount;
        }

        #endregion

        private static List<MenuItemInfo> _cachedMenuItems;
        private static DateTime _cacheTime;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public static RpcResponse ExecuteMenuItem(RpcRequest request)
        {
            var req = request.GetParams<ExecuteMenuItemRequest>();

            if (string.IsNullOrEmpty(req.menuPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "menuPath is required");
            }

            try
            {
                // Check if menu item exists
                var exists = EditorApplication.ExecuteMenuItem(req.menuPath);
                
                if (!exists)
                {
                    // Try to find similar menu items for suggestion
                    var similar = FindSimilarMenuItems(req.menuPath, 5);
                    var suggestions = similar.Any() 
                        ? $" Did you mean: {string.Join(", ", similar.Select(m => $"'{m.path}'"))}"
                        : "";
                    
                    return RpcResponse.Failure("MENU_ITEM_NOT_FOUND", 
                        $"Menu item '{req.menuPath}' not found.{suggestions}");
                }

                return RpcResponse.Success(new ExecuteMenuItemResponse
                {
                    success = true,
                    menuPath = req.menuPath
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("EXECUTION_ERROR", $"Failed to execute menu item: {ex.Message}");
            }
        }

        public static RpcResponse ListMenuItems(RpcRequest request)
        {
            var req = request.GetParams<ListMenuItemsRequest>();

            // Refresh cache if needed
            if (_cachedMenuItems == null || DateTime.Now - _cacheTime > CacheDuration)
            {
                RefreshMenuItemCache();
            }

            var filter = req.filter?.ToLowerInvariant() ?? "";
            var filtered = _cachedMenuItems
                .Where(m => string.IsNullOrEmpty(filter) || m.path.ToLowerInvariant().Contains(filter))
                .Take(req.maxResults)
                .ToList();

            return RpcResponse.Success(new ListMenuItemsResponse
            {
                items = filtered,
                totalCount = _cachedMenuItems.Count(m => string.IsNullOrEmpty(filter) || m.path.ToLowerInvariant().Contains(filter))
            });
        }

        private static void RefreshMenuItemCache()
        {
            _cachedMenuItems = new List<MenuItemInfo>();

            // Use reflection to find all MenuItem attributes
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var menuItemAttr = method.GetCustomAttribute<MenuItem>();
                            if (menuItemAttr != null)
                            {
                                var path = menuItemAttr.menuItem;
                                
                                // Extract shortcut if present (format: "Menu/Item %g" where %g is shortcut)
                                string shortcut = null;
                                bool hasShortcut = false;
                                
                                var shortcutIndex = path.LastIndexOf(' ');
                                if (shortcutIndex > 0)
                                {
                                    var possibleShortcut = path.Substring(shortcutIndex + 1);
                                    if (possibleShortcut.StartsWith("%") || possibleShortcut.StartsWith("#") || 
                                        possibleShortcut.StartsWith("&") || possibleShortcut.StartsWith("_"))
                                    {
                                        shortcut = possibleShortcut;
                                        hasShortcut = true;
                                        path = path.Substring(0, shortcutIndex);
                                    }
                                }

                                _cachedMenuItems.Add(new MenuItemInfo
                                {
                                    path = path,
                                    hasShortcut = hasShortcut,
                                    shortcut = shortcut,
                                    priority = menuItemAttr.priority
                                });
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                }
                catch (Exception)
                {
                    // Skip problematic assemblies
                }
            }

            // Sort by path
            _cachedMenuItems = _cachedMenuItems
                .OrderBy(m => m.path)
                .Distinct(new MenuItemComparer())
                .ToList();

            _cacheTime = DateTime.Now;
            Debug.Log($"[UnityVision] Cached {_cachedMenuItems.Count} menu items");
        }

        private static List<MenuItemInfo> FindSimilarMenuItems(string path, int maxResults)
        {
            if (_cachedMenuItems == null) RefreshMenuItemCache();

            var pathLower = path.ToLowerInvariant();
            var lastPart = path.Split('/').Last().ToLowerInvariant();

            return _cachedMenuItems
                .Where(m => m.path.ToLowerInvariant().Contains(lastPart) || 
                           LevenshteinDistance(m.path.ToLowerInvariant(), pathLower) < 10)
                .OrderBy(m => LevenshteinDistance(m.path.ToLowerInvariant(), pathLower))
                .Take(maxResults)
                .ToList();
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                }
            }

            return d[s1.Length, s2.Length];
        }

        private class MenuItemComparer : IEqualityComparer<MenuItemInfo>
        {
            public bool Equals(MenuItemInfo x, MenuItemInfo y) => x?.path == y?.path;
            public int GetHashCode(MenuItemInfo obj) => obj?.path?.GetHashCode() ?? 0;
        }
    }
}
