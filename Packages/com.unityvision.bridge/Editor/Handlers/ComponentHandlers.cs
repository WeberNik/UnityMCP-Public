// ============================================================================
// UnityVision Bridge - Component Handlers
// Handlers for adding components and setting properties
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityVision.Editor.Bridge;
using Newtonsoft.Json.Linq;

namespace UnityVision.Editor.Handlers
{
    public static class ComponentHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class AddComponentRequest
        {
            public string gameObjectPath;
            public string componentType;
            public bool dryRun = false;
        }

        [Serializable]
        public class AddComponentResponse
        {
            public bool success;
            public string componentId;
            public object dryRunPlan;
        }

        [Serializable]
        public class SetComponentPropertiesRequest
        {
            public string gameObjectPath;
            public string componentType;
            public Dictionary<string, object> properties;
            public bool dryRun = false;
        }

        [Serializable]
        public class SetComponentPropertiesResponse
        {
            public bool success;
            public List<string> modifiedProperties;
            public List<string> errors;
            public object dryRunPlan;
        }

        [Serializable]
        public class SearchComponentTypesRequest
        {
            public string query = "";
            public int maxResults = 50;
            public bool includeUnityEngine = true;
            public bool includeUnityEditor = false;
            public bool includeUserAssemblies = true;
        }

        [Serializable]
        public class ComponentTypeInfo
        {
            public string fullName;
            public string shortName;
            public string assemblyName;
            public string @namespace;
        }

        [Serializable]
        public class SearchComponentTypesResponse
        {
            public List<ComponentTypeInfo> results;
            public int totalMatches;
        }

        #endregion

        public static RpcResponse SearchComponentTypes(RpcRequest request)
        {
            var req = request.GetParams<SearchComponentTypesRequest>();
            var results = new List<ComponentTypeInfo>();
            int totalMatches = 0;

            var query = req.query?.ToLowerInvariant() ?? "";
            var componentType = typeof(Component);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                
                // Filter assemblies
                bool isUnityEngine = assemblyName.StartsWith("UnityEngine");
                bool isUnityEditor = assemblyName.StartsWith("UnityEditor");
                bool isUserAssembly = !isUnityEngine && !isUnityEditor && 
                                      !assemblyName.StartsWith("System") && 
                                      !assemblyName.StartsWith("mscorlib") &&
                                      !assemblyName.StartsWith("netstandard") &&
                                      !assemblyName.StartsWith("Newtonsoft");

                if (isUnityEngine && !req.includeUnityEngine) continue;
                if (isUnityEditor && !req.includeUnityEditor) continue;
                if (isUserAssembly && !req.includeUserAssemblies) continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!componentType.IsAssignableFrom(type)) continue;
                        if (type.IsAbstract) continue;
                        if (type.IsGenericTypeDefinition) continue;

                        var fullName = type.FullName ?? type.Name;
                        var shortName = type.Name;

                        // Match query
                        if (!string.IsNullOrEmpty(query))
                        {
                            if (!fullName.ToLowerInvariant().Contains(query) &&
                                !shortName.ToLowerInvariant().Contains(query))
                            {
                                continue;
                            }
                        }

                        totalMatches++;

                        if (results.Count < req.maxResults)
                        {
                            results.Add(new ComponentTypeInfo
                            {
                                fullName = fullName,
                                shortName = shortName,
                                assemblyName = assemblyName,
                                @namespace = type.Namespace ?? ""
                            });
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be loaded
                }
            }

            // Sort by relevance (exact match first, then by name length)
            results.Sort((a, b) =>
            {
                bool aExact = a.shortName.Equals(req.query, StringComparison.OrdinalIgnoreCase);
                bool bExact = b.shortName.Equals(req.query, StringComparison.OrdinalIgnoreCase);
                if (aExact && !bExact) return -1;
                if (bExact && !aExact) return 1;
                return a.shortName.Length.CompareTo(b.shortName.Length);
            });

            return RpcResponse.Success(new SearchComponentTypesResponse
            {
                results = results,
                totalMatches = totalMatches
            });
        }

        public static RpcResponse AddComponent(RpcRequest request)
        {
            var req = request.GetParams<AddComponentRequest>();

            if (string.IsNullOrEmpty(req.gameObjectPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "gameObjectPath is required");
            }

            if (string.IsNullOrEmpty(req.componentType))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "componentType is required");
            }

            var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
            if (go == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
            }

            var type = FindType(req.componentType);
            if (type == null)
            {
                return RpcResponse.Failure("TYPE_NOT_FOUND", $"Component type not found: {req.componentType}");
            }

            // Dry run
            if (req.dryRun)
            {
                return RpcResponse.Success(new AddComponentResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldAdd = req.componentType,
                        to = req.gameObjectPath
                    }
                });
            }

            var component = Undo.AddComponent(go, type);
            EditorSceneManager.MarkSceneDirty(go.scene);

            return RpcResponse.Success(new AddComponentResponse
            {
                success = true,
                componentId = $"cmp_{component.GetInstanceID()}"
            });
        }

        public static RpcResponse SetComponentProperties(RpcRequest request)
        {
            var req = request.GetParams<SetComponentPropertiesRequest>();

            if (string.IsNullOrEmpty(req.gameObjectPath))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "gameObjectPath is required");
            }

            if (string.IsNullOrEmpty(req.componentType))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "componentType is required");
            }

            if (req.properties == null || req.properties.Count == 0)
            {
                return RpcResponse.Failure("INVALID_PARAMS", "properties is required");
            }

            var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
            if (go == null)
            {
                return RpcResponse.Failure("GAMEOBJECT_NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
            }

            var type = FindType(req.componentType);
            if (type == null)
            {
                return RpcResponse.Failure("TYPE_NOT_FOUND", $"Component type not found: {req.componentType}");
            }

            var component = go.GetComponent(type);
            if (component == null)
            {
                return RpcResponse.Failure("COMPONENT_NOT_FOUND",
                    $"Component {req.componentType} not found on {req.gameObjectPath}");
            }

            // Dry run
            if (req.dryRun)
            {
                return RpcResponse.Success(new SetComponentPropertiesResponse
                {
                    success = true,
                    dryRunPlan = new
                    {
                        wouldModify = $"{req.gameObjectPath}:{req.componentType}",
                        properties = req.properties
                    }
                });
            }

            // Use SerializedObject for proper undo support
            var serializedObject = new SerializedObject(component);
            var modifiedProperties = new List<string>();
            var errors = new List<string>();

            foreach (var kvp in req.properties)
            {
                var prop = serializedObject.FindProperty(kvp.Key);
                if (prop != null)
                {
                    var (success, error) = SetSerializedPropertyValue(prop, kvp.Value);
                    if (success)
                    {
                        modifiedProperties.Add(kvp.Key);
                    }
                    else if (error != null)
                    {
                        errors.Add(error);
                    }
                }
                else
                {
                    // Try reflection for non-serialized properties
                    var fieldInfo = type.GetField(kvp.Key,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var propInfo = type.GetProperty(kvp.Key,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        try
                        {
                            Undo.RecordObject(component, $"Set {kvp.Key}");
                            fieldInfo.SetValue(component, ConvertValue(kvp.Value, fieldInfo.FieldType));
                            modifiedProperties.Add(kvp.Key);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to set field '{kvp.Key}': {ex.Message}");
                        }
                    }
                    else if (propInfo != null && propInfo.CanWrite)
                    {
                        try
                        {
                            Undo.RecordObject(component, $"Set {kvp.Key}");
                            propInfo.SetValue(component, ConvertValue(kvp.Value, propInfo.PropertyType));
                            modifiedProperties.Add(kvp.Key);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"Failed to set property '{kvp.Key}': {ex.Message}");
                        }
                    }
                    else
                    {
                        errors.Add($"Property '{kvp.Key}' not found on component '{req.componentType}'");
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(go.scene);

            // Return partial success with errors if any
            if (errors.Count > 0 && modifiedProperties.Count == 0)
            {
                return RpcResponse.Failure("PROPERTY_SET_FAILED", 
                    $"Failed to set properties: {string.Join("; ", errors)}",
                    new { errors });
            }

            return RpcResponse.Success(new SetComponentPropertiesResponse
            {
                success = true,
                modifiedProperties = modifiedProperties,
                errors = errors.Count > 0 ? errors : null
            });
        }

        #region Helpers

        // Type cache for performance
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private static readonly object _typeCacheLock = new object();

        private static Type FindType(string typeName)
        {
            // Check cache first
            lock (_typeCacheLock)
            {
                if (_typeCache.TryGetValue(typeName, out var cachedType))
                {
                    return cachedType;
                }
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                CacheType(typeName, type);
                return type;
            }

            // Try common Unity namespaces first for faster lookup
            var commonNamespaces = new[] { "UnityEngine", "UnityEngine.UI", "UnityEditor" };
            foreach (var ns in commonNamespaces)
            {
                type = Type.GetType($"{ns}.{typeName}, {ns}");
                if (type != null)
                {
                    CacheType(typeName, type);
                    return type;
                }
            }

            // Fall back to searching all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(typeName);
                if (type != null)
                {
                    CacheType(typeName, type);
                    return type;
                }
            }

            return null;
        }

        private static void CacheType(string typeName, Type type)
        {
            lock (_typeCacheLock)
            {
                _typeCache[typeName] = type;
            }
        }

        private static (bool success, string error) SetSerializedPropertyValue(SerializedProperty prop, object value)
        {
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        if (!TryConvertToInt(value, out int intVal))
                            return (false, $"Cannot convert '{value}' to Integer for property '{prop.name}'");
                        prop.intValue = intVal;
                        return (true, null);
                        
                    case SerializedPropertyType.Boolean:
                        if (!TryConvertToBool(value, out bool boolVal))
                            return (false, $"Cannot convert '{value}' to Boolean for property '{prop.name}'");
                        prop.boolValue = boolVal;
                        return (true, null);
                        
                    case SerializedPropertyType.Float:
                        if (!TryConvertToFloat(value, out float floatVal))
                            return (false, $"Cannot convert '{value}' to Float for property '{prop.name}'");
                        prop.floatValue = floatVal;
                        return (true, null);
                        
                    case SerializedPropertyType.String:
                        prop.stringValue = value?.ToString() ?? "";
                        return (true, null);
                        
                    case SerializedPropertyType.Color:
                        if (value is JObject colorObj)
                        {
                            prop.colorValue = new Color(
                                colorObj["r"]?.Value<float>() ?? 0,
                                colorObj["g"]?.Value<float>() ?? 0,
                                colorObj["b"]?.Value<float>() ?? 0,
                                colorObj["a"]?.Value<float>() ?? 1
                            );
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects Color object with r,g,b,a fields");
                        
                    case SerializedPropertyType.Vector2:
                        if (value is JObject v2Obj)
                        {
                            prop.vector2Value = new Vector2(
                                v2Obj["x"]?.Value<float>() ?? 0,
                                v2Obj["y"]?.Value<float>() ?? 0
                            );
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects Vector2 object with x,y fields");
                        
                    case SerializedPropertyType.Vector3:
                        if (value is JObject v3Obj)
                        {
                            prop.vector3Value = new Vector3(
                                v3Obj["x"]?.Value<float>() ?? 0,
                                v3Obj["y"]?.Value<float>() ?? 0,
                                v3Obj["z"]?.Value<float>() ?? 0
                            );
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects Vector3 object with x,y,z fields");
                        
                    case SerializedPropertyType.Vector4:
                        if (value is JObject v4Obj)
                        {
                            prop.vector4Value = new Vector4(
                                v4Obj["x"]?.Value<float>() ?? 0,
                                v4Obj["y"]?.Value<float>() ?? 0,
                                v4Obj["z"]?.Value<float>() ?? 0,
                                v4Obj["w"]?.Value<float>() ?? 0
                            );
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects Vector4 object with x,y,z,w fields");
                        
                    case SerializedPropertyType.Quaternion:
                        if (value is JObject quatObj)
                        {
                            prop.quaternionValue = new Quaternion(
                                quatObj["x"]?.Value<float>() ?? 0,
                                quatObj["y"]?.Value<float>() ?? 0,
                                quatObj["z"]?.Value<float>() ?? 0,
                                quatObj["w"]?.Value<float>() ?? 1
                            );
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects Quaternion object with x,y,z,w fields");
                        
                    case SerializedPropertyType.Rect:
                        if (value is JObject rectObj)
                        {
                            prop.rectValue = new Rect(
                                rectObj["x"]?.Value<float>() ?? 0,
                                rectObj["y"]?.Value<float>() ?? 0,
                                rectObj["width"]?.Value<float>() ?? 0,
                                rectObj["height"]?.Value<float>() ?? 0
                            );
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects Rect object with x,y,width,height fields");
                        
                    case SerializedPropertyType.ObjectReference:
                        if (value is string path)
                        {
                            var refGo = GameObjectHandlers.FindGameObjectByPath(path);
                            if (refGo != null)
                            {
                                prop.objectReferenceValue = refGo;
                                return (true, null);
                            }
                            return (false, $"GameObject not found at path '{path}' for property '{prop.name}'");
                        }
                        if (value == null)
                        {
                            prop.objectReferenceValue = null;
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects a GameObject path string");
                        
                    case SerializedPropertyType.Enum:
                        if (value is string enumName)
                        {
                            var enumNames = prop.enumNames;
                            var index = Array.IndexOf(enumNames, enumName);
                            if (index >= 0)
                            {
                                prop.enumValueIndex = index;
                                return (true, null);
                            }
                            return (false, $"Invalid enum value '{enumName}' for property '{prop.name}'. Valid values: {string.Join(", ", enumNames)}");
                        }
                        if (value is int || value is long)
                        {
                            prop.enumValueIndex = Convert.ToInt32(value);
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects enum name string or integer index");
                        
                    case SerializedPropertyType.LayerMask:
                        if (TryConvertToInt(value, out int layerVal))
                        {
                            prop.intValue = layerVal;
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects LayerMask integer value");
                        
                    case SerializedPropertyType.ArraySize:
                        if (TryConvertToInt(value, out int sizeVal))
                        {
                            prop.arraySize = sizeVal;
                            return (true, null);
                        }
                        return (false, $"Property '{prop.name}' expects integer array size");
                        
                    default:
                        return (false, $"Unsupported property type '{prop.propertyType}' for property '{prop.name}'");
                }
            }
            catch (InvalidCastException ex)
            {
                return (false, $"Type mismatch for property '{prop.name}': {ex.Message}");
            }
            catch (FormatException ex)
            {
                return (false, $"Format error for property '{prop.name}': {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return (false, $"Invalid argument for property '{prop.name}': {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityVision] Failed to set property {prop.name}: {ex.Message}");
                return (false, $"Unexpected error setting property '{prop.name}': {ex.Message}");
            }
        }
        
        private static bool TryConvertToInt(object value, out int result)
        {
            result = 0;
            if (value == null) return false;
            try
            {
                result = Convert.ToInt32(value);
                return true;
            }
            catch { return false; }
        }
        
        private static bool TryConvertToFloat(object value, out float result)
        {
            result = 0f;
            if (value == null) return false;
            try
            {
                result = Convert.ToSingle(value);
                return true;
            }
            catch { return false; }
        }
        
        private static bool TryConvertToBool(object value, out bool result)
        {
            result = false;
            if (value == null) return false;
            if (value is bool b) { result = b; return true; }
            if (value is string s)
            {
                if (bool.TryParse(s, out result)) return true;
                if (s == "1") { result = true; return true; }
                if (s == "0") { result = false; return true; }
            }
            try
            {
                result = Convert.ToBoolean(value);
                return true;
            }
            catch { return false; }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType == typeof(string))
                return value.ToString();

            if (targetType.IsPrimitive)
                return Convert.ChangeType(value, targetType);

            if (value is JObject jobj)
                return jobj.ToObject(targetType);

            return value;
        }

        #endregion
    }
}
