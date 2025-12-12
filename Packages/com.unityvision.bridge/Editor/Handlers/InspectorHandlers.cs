// ============================================================================
// UnityVision Bridge - Inspector Handlers
// Handlers for reading/writing component properties at runtime
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class InspectorHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetComponentPropertiesRequest
        {
            public string gameObjectPath;
            public string targetId; // Alias for gameObjectPath (accepts go_ IDs)
            public string componentType;
            public bool includePrivate = false;
            public int maxDepth = 2;
            
            // Helper to get the actual path/id to use
            public string GetPathOrId() => !string.IsNullOrEmpty(targetId) ? targetId : gameObjectPath;
        }

        [Serializable]
        public class GetComponentPropertiesResponse
        {
            public string gameObjectPath;
            public string componentType;
            public string componentFullType;
            public List<PropertyInfo> properties;
        }

        [Serializable]
        public class PropertyInfo
        {
            public string name;
            public string type;
            public string value;
            public bool isReadOnly;
            public bool isArray;
            public int arrayLength;
            public string tooltip;
            public float? rangeMin;
            public float? rangeMax;
        }

        [Serializable]
        public class SetComponentPropertyRequest
        {
            public string gameObjectPath;
            public string targetId; // Alias for gameObjectPath (accepts go_ IDs)
            public string componentType;
            public string propertyName;
            public string value;
            public bool recordUndo = true;
            
            public string GetPathOrId() => !string.IsNullOrEmpty(targetId) ? targetId : gameObjectPath;
        }

        [Serializable]
        public class SetComponentPropertyResponse
        {
            public bool success;
            public string previousValue;
            public string newValue;
        }

        [Serializable]
        public class CompareComponentsRequest
        {
            public string gameObjectPath1;
            public string gameObjectPath2;
            public string componentType;
        }

        [Serializable]
        public class CompareComponentsResponse
        {
            public List<PropertyDiff> differences;
            public int totalProperties;
            public int differentCount;
        }

        [Serializable]
        public class PropertyDiff
        {
            public string propertyName;
            public string value1;
            public string value2;
        }

        #endregion

        public static RpcResponse GetComponentProperties(RpcRequest request)
        {
            var req = request.GetParams<GetComponentPropertiesRequest>();

            try
            {
                var pathOrId = req.GetPathOrId();
                var go = GameObjectHandlers.FindGameObjectByPath(pathOrId);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {pathOrId}");
                }

                // Find component
                Component component = null;
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    if (comp.GetType().Name == req.componentType || 
                        comp.GetType().FullName == req.componentType)
                    {
                        component = comp;
                        break;
                    }
                }

                if (component == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Component not found: {req.componentType}");
                }

                var response = new GetComponentPropertiesResponse
                {
                    gameObjectPath = GameObjectHandlers.GetGameObjectPath(go), // Always return resolved path
                    componentType = component.GetType().Name,
                    componentFullType = component.GetType().FullName,
                    properties = new List<PropertyInfo>()
                };

                // Use SerializedObject for proper Unity serialization
                var serializedObject = new SerializedObject(component);
                var iterator = serializedObject.GetIterator();
                
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        // Skip script reference
                        if (iterator.name == "m_Script") continue;

                        var propInfo = new PropertyInfo
                        {
                            name = iterator.displayName,
                            type = iterator.propertyType.ToString(),
                            isReadOnly = false,
                            isArray = iterator.isArray,
                            arrayLength = iterator.isArray ? iterator.arraySize : 0
                        };

                        // Get value as string
                        propInfo.value = GetSerializedPropertyValue(iterator);

                        // Try to get tooltip from field
                        var field = component.GetType().GetField(iterator.name, 
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null)
                        {
                            var tooltipAttr = field.GetCustomAttribute<TooltipAttribute>();
                            if (tooltipAttr != null)
                            {
                                propInfo.tooltip = tooltipAttr.tooltip;
                            }

                            var rangeAttr = field.GetCustomAttribute<RangeAttribute>();
                            if (rangeAttr != null)
                            {
                                propInfo.rangeMin = rangeAttr.min;
                                propInfo.rangeMax = rangeAttr.max;
                            }
                        }

                        response.properties.Add(propInfo);

                    } while (iterator.NextVisible(false));
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("INSPECTOR_ERROR", ex.Message);
            }
        }

        public static RpcResponse SetComponentProperty(RpcRequest request)
        {
            var req = request.GetParams<SetComponentPropertyRequest>();

            try
            {
                var pathOrId = req.GetPathOrId();
                var go = GameObjectHandlers.FindGameObjectByPath(pathOrId);
                if (go == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {pathOrId}");
                }

                // Find component
                Component component = null;
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    if (comp.GetType().Name == req.componentType || 
                        comp.GetType().FullName == req.componentType)
                    {
                        component = comp;
                        break;
                    }
                }

                if (component == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Component not found: {req.componentType}");
                }

                var serializedObject = new SerializedObject(component);
                var property = serializedObject.FindProperty(req.propertyName);

                if (property == null)
                {
                    // Try to find by display name
                    var iterator = serializedObject.GetIterator();
                    if (iterator.NextVisible(true))
                    {
                        do
                        {
                            if (iterator.displayName == req.propertyName || iterator.name == req.propertyName)
                            {
                                property = iterator.Copy();
                                break;
                            }
                        } while (iterator.NextVisible(false));
                    }
                }

                if (property == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Property not found: {req.propertyName}");
                }

                string previousValue = GetSerializedPropertyValue(property);

                // Record undo
                if (req.recordUndo)
                {
                    Undo.RecordObject(component, $"Set {req.propertyName}");
                }

                // Set value based on type
                bool success = SetSerializedPropertyValue(property, req.value);

                if (success)
                {
                    serializedObject.ApplyModifiedProperties();
                    
                    return RpcResponse.Success(new SetComponentPropertyResponse
                    {
                        success = true,
                        previousValue = previousValue,
                        newValue = req.value
                    });
                }
                else
                {
                    return RpcResponse.Failure("SET_FAILED", $"Failed to set property value");
                }
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("INSPECTOR_ERROR", ex.Message);
            }
        }

        public static RpcResponse CompareComponents(RpcRequest request)
        {
            var req = request.GetParams<CompareComponentsRequest>();

            try
            {
                var go1 = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath1);
                var go2 = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath2);

                if (go1 == null) return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath1}");
                if (go2 == null) return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath2}");

                Component comp1 = null, comp2 = null;

                foreach (var c in go1.GetComponents<Component>())
                {
                    if (c != null && (c.GetType().Name == req.componentType || c.GetType().FullName == req.componentType))
                    {
                        comp1 = c;
                        break;
                    }
                }

                foreach (var c in go2.GetComponents<Component>())
                {
                    if (c != null && (c.GetType().Name == req.componentType || c.GetType().FullName == req.componentType))
                    {
                        comp2 = c;
                        break;
                    }
                }

                if (comp1 == null) return RpcResponse.Failure("NOT_FOUND", $"Component {req.componentType} not found on {req.gameObjectPath1}");
                if (comp2 == null) return RpcResponse.Failure("NOT_FOUND", $"Component {req.componentType} not found on {req.gameObjectPath2}");

                var response = new CompareComponentsResponse
                {
                    differences = new List<PropertyDiff>(),
                    totalProperties = 0
                };

                var so1 = new SerializedObject(comp1);
                var so2 = new SerializedObject(comp2);

                var iter1 = so1.GetIterator();
                if (iter1.NextVisible(true))
                {
                    do
                    {
                        if (iter1.name == "m_Script") continue;
                        response.totalProperties++;

                        var prop2 = so2.FindProperty(iter1.name);
                        if (prop2 != null)
                        {
                            string val1 = GetSerializedPropertyValue(iter1);
                            string val2 = GetSerializedPropertyValue(prop2);

                            if (val1 != val2)
                            {
                                response.differences.Add(new PropertyDiff
                                {
                                    propertyName = iter1.displayName,
                                    value1 = val1,
                                    value2 = val2
                                });
                            }
                        }
                    } while (iter1.NextVisible(false));
                }

                response.differentCount = response.differences.Count;

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("COMPARE_ERROR", ex.Message);
            }
        }

        #region Helper Methods

        private static string GetSerializedPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Boolean:
                    return prop.boolValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString("F4");
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    return $"RGBA({prop.colorValue.r:F2}, {prop.colorValue.g:F2}, {prop.colorValue.b:F2}, {prop.colorValue.a:F2})";
                case SerializedPropertyType.Vector2:
                    return $"({prop.vector2Value.x:F2}, {prop.vector2Value.y:F2})";
                case SerializedPropertyType.Vector3:
                    return $"({prop.vector3Value.x:F2}, {prop.vector3Value.y:F2}, {prop.vector3Value.z:F2})";
                case SerializedPropertyType.Vector4:
                    return $"({prop.vector4Value.x:F2}, {prop.vector4Value.y:F2}, {prop.vector4Value.z:F2}, {prop.vector4Value.w:F2})";
                case SerializedPropertyType.Quaternion:
                    var euler = prop.quaternionValue.eulerAngles;
                    return $"({euler.x:F2}, {euler.y:F2}, {euler.z:F2})";
                case SerializedPropertyType.Enum:
                    return prop.enumDisplayNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "None";
                case SerializedPropertyType.LayerMask:
                    return LayerMask.LayerToName(prop.intValue);
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Bounds:
                    return $"Center: {prop.boundsValue.center}, Size: {prop.boundsValue.size}";
                case SerializedPropertyType.Rect:
                    return $"({prop.rectValue.x:F2}, {prop.rectValue.y:F2}, {prop.rectValue.width:F2}, {prop.rectValue.height:F2})";
                default:
                    if (prop.isArray)
                    {
                        return $"[Array: {prop.arraySize} elements]";
                    }
                    return $"[{prop.propertyType}]";
            }
        }

        private static bool SetSerializedPropertyValue(SerializedProperty prop, string value)
        {
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = int.Parse(value);
                        return true;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = bool.Parse(value);
                        return true;
                    case SerializedPropertyType.Float:
                        prop.floatValue = float.Parse(value);
                        return true;
                    case SerializedPropertyType.String:
                        prop.stringValue = value;
                        return true;
                    case SerializedPropertyType.Color:
                        prop.colorValue = ParseColor(value);
                        return true;
                    case SerializedPropertyType.Vector2:
                        prop.vector2Value = ParseVector2(value);
                        return true;
                    case SerializedPropertyType.Vector3:
                        prop.vector3Value = ParseVector3(value);
                        return true;
                    case SerializedPropertyType.Enum:
                        // Try to find enum by name
                        for (int i = 0; i < prop.enumDisplayNames.Length; i++)
                        {
                            if (prop.enumDisplayNames[i].Equals(value, StringComparison.OrdinalIgnoreCase))
                            {
                                prop.enumValueIndex = i;
                                return true;
                            }
                        }
                        // Try as int
                        if (int.TryParse(value, out int enumIndex))
                        {
                            prop.enumValueIndex = enumIndex;
                            return true;
                        }
                        return false;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static Color ParseColor(string value)
        {
            // Handle "RGBA(r, g, b, a)" or "r, g, b, a" or "#RRGGBB"
            if (value.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(value, out Color c);
                return c;
            }

            value = value.Replace("RGBA(", "").Replace("RGB(", "").Replace(")", "");
            var parts = value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            
            if (parts.Length >= 4)
                return new Color(parts[0], parts[1], parts[2], parts[3]);
            if (parts.Length >= 3)
                return new Color(parts[0], parts[1], parts[2], 1f);
            
            return Color.white;
        }

        private static Vector2 ParseVector2(string value)
        {
            value = value.Replace("(", "").Replace(")", "");
            var parts = value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            return new Vector2(parts[0], parts[1]);
        }

        private static Vector3 ParseVector3(string value)
        {
            value = value.Replace("(", "").Replace(")", "");
            var parts = value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            return new Vector3(parts[0], parts[1], parts[2]);
        }

        #endregion
    }
}
