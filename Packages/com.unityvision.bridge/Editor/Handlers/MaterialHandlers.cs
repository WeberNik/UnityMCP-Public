// ============================================================================
// UnityVision Bridge - Material Handlers
// Handlers for material and shader property inspection/modification
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class MaterialHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class GetMaterialPropertiesRequest
        {
            public string materialPath;      // Asset path OR
            public string gameObjectPath;    // GameObject with Renderer
            public int materialIndex = 0;    // Which material on renderer
        }

        [Serializable]
        public class GetMaterialPropertiesResponse
        {
            public string materialName;
            public string shaderName;
            public List<MaterialProperty> properties;
            public List<string> shaderKeywords;
            public int renderQueue;
        }

        [Serializable]
        public class MaterialProperty
        {
            public string name;
            public string displayName;
            public string type;  // Color, Float, Range, Texture, Vector
            public string value;
            public float? rangeMin;
            public float? rangeMax;
            public string textureValue;  // For textures: asset path
        }

        [Serializable]
        public class SetMaterialPropertyRequest
        {
            public string materialPath;
            public string gameObjectPath;
            public int materialIndex = 0;
            public string propertyName;
            public string value;
            public bool createInstance = false;  // Create material instance for runtime changes
        }

        [Serializable]
        public class SetMaterialPropertyResponse
        {
            public bool success;
            public string previousValue;
            public string newValue;
            public bool createdInstance;
        }

        [Serializable]
        public class ListMaterialsRequest
        {
            public string folder = "Assets";
            public string shaderFilter;
            public string nameFilter;
            public int maxResults = 100;
        }

        [Serializable]
        public class ListMaterialsResponse
        {
            public int totalCount;
            public List<MaterialInfo> materials;
        }

        [Serializable]
        public class MaterialInfo
        {
            public string path;
            public string name;
            public string shaderName;
            public int renderQueue;
        }

        [Serializable]
        public class ListShadersResponse
        {
            public List<ShaderInfo> shaders;
        }

        [Serializable]
        public class ShaderInfo
        {
            public string name;
            public int propertyCount;
            public bool isSupported;
        }

        #endregion

        public static RpcResponse GetMaterialProperties(RpcRequest request)
        {
            var req = request.GetParams<GetMaterialPropertiesRequest>();

            try
            {
                Material material = null;

                // Get material from asset path
                if (!string.IsNullOrEmpty(req.materialPath))
                {
                    material = AssetDatabase.LoadAssetAtPath<Material>(req.materialPath);
                }
                // Get material from GameObject's Renderer
                else if (!string.IsNullOrEmpty(req.gameObjectPath))
                {
                    var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                    if (go == null)
                    {
                        return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                    }

                    var renderer = go.GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        return RpcResponse.Failure("NOT_FOUND", "GameObject has no Renderer component");
                    }

                    var materials = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                    if (req.materialIndex >= materials.Length)
                    {
                        return RpcResponse.Failure("INDEX_OUT_OF_RANGE", $"Material index {req.materialIndex} out of range (has {materials.Length})");
                    }

                    material = materials[req.materialIndex];
                }

                if (material == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", "Material not found");
                }

                var response = new GetMaterialPropertiesResponse
                {
                    materialName = material.name,
                    shaderName = material.shader != null ? material.shader.name : "None",
                    properties = new List<MaterialProperty>(),
                    shaderKeywords = material.shaderKeywords.ToList(),
                    renderQueue = material.renderQueue
                };

                // Get shader properties
                if (material.shader != null)
                {
                    int propCount = ShaderUtil.GetPropertyCount(material.shader);
                    for (int i = 0; i < propCount; i++)
                    {
                        string propName = ShaderUtil.GetPropertyName(material.shader, i);
                        var propType = ShaderUtil.GetPropertyType(material.shader, i);
                        string propDesc = ShaderUtil.GetPropertyDescription(material.shader, i);

                        var prop = new MaterialProperty
                        {
                            name = propName,
                            displayName = propDesc,
                            type = propType.ToString()
                        };

                        switch (propType)
                        {
                            case ShaderUtil.ShaderPropertyType.Color:
                                var color = material.GetColor(propName);
                                prop.value = $"RGBA({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})";
                                break;

                            case ShaderUtil.ShaderPropertyType.Float:
                                prop.value = material.GetFloat(propName).ToString("F4");
                                break;

                            case ShaderUtil.ShaderPropertyType.Range:
                                prop.value = material.GetFloat(propName).ToString("F4");
                                prop.rangeMin = ShaderUtil.GetRangeLimits(material.shader, i, 1);
                                prop.rangeMax = ShaderUtil.GetRangeLimits(material.shader, i, 2);
                                break;

                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                var tex = material.GetTexture(propName);
                                prop.value = tex != null ? tex.name : "None";
                                if (tex != null)
                                {
                                    prop.textureValue = AssetDatabase.GetAssetPath(tex);
                                }
                                break;

                            case ShaderUtil.ShaderPropertyType.Vector:
                                var vec = material.GetVector(propName);
                                prop.value = $"({vec.x:F3}, {vec.y:F3}, {vec.z:F3}, {vec.w:F3})";
                                break;
                        }

                        response.properties.Add(prop);
                    }
                }

                return RpcResponse.Success(response);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("MATERIAL_ERROR", ex.Message);
            }
        }

        public static RpcResponse SetMaterialProperty(RpcRequest request)
        {
            var req = request.GetParams<SetMaterialPropertyRequest>();

            try
            {
                Material material = null;
                Renderer renderer = null;
                bool createdInstance = false;

                // Get material
                if (!string.IsNullOrEmpty(req.materialPath))
                {
                    material = AssetDatabase.LoadAssetAtPath<Material>(req.materialPath);
                }
                else if (!string.IsNullOrEmpty(req.gameObjectPath))
                {
                    var go = GameObjectHandlers.FindGameObjectByPath(req.gameObjectPath);
                    if (go == null)
                    {
                        return RpcResponse.Failure("NOT_FOUND", $"GameObject not found: {req.gameObjectPath}");
                    }

                    renderer = go.GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        return RpcResponse.Failure("NOT_FOUND", "GameObject has no Renderer component");
                    }

                    if (req.createInstance || Application.isPlaying)
                    {
                        // Create instance to avoid modifying shared material
                        material = renderer.materials[req.materialIndex];
                        createdInstance = true;
                    }
                    else
                    {
                        material = renderer.sharedMaterials[req.materialIndex];
                    }
                }

                if (material == null)
                {
                    return RpcResponse.Failure("NOT_FOUND", "Material not found");
                }

                // Find property type
                if (material.shader == null)
                {
                    return RpcResponse.Failure("NO_SHADER", "Material has no shader");
                }

                int propIndex = -1;
                ShaderUtil.ShaderPropertyType propType = ShaderUtil.ShaderPropertyType.Float;
                
                int propCount = ShaderUtil.GetPropertyCount(material.shader);
                for (int i = 0; i < propCount; i++)
                {
                    if (ShaderUtil.GetPropertyName(material.shader, i) == req.propertyName)
                    {
                        propIndex = i;
                        propType = ShaderUtil.GetPropertyType(material.shader, i);
                        break;
                    }
                }

                if (propIndex < 0)
                {
                    return RpcResponse.Failure("NOT_FOUND", $"Property not found: {req.propertyName}");
                }

                // Get previous value
                string previousValue = GetPropertyValueString(material, req.propertyName, propType);

                // Record undo
                Undo.RecordObject(material, $"Set {req.propertyName}");

                // Set value
                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        material.SetColor(req.propertyName, ParseColor(req.value));
                        break;

                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        material.SetFloat(req.propertyName, float.Parse(req.value));
                        break;

                    case ShaderUtil.ShaderPropertyType.Vector:
                        material.SetVector(req.propertyName, ParseVector4(req.value));
                        break;

                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var texture = AssetDatabase.LoadAssetAtPath<Texture>(req.value);
                        material.SetTexture(req.propertyName, texture);
                        break;
                }

                EditorUtility.SetDirty(material);

                return RpcResponse.Success(new SetMaterialPropertyResponse
                {
                    success = true,
                    previousValue = previousValue,
                    newValue = req.value,
                    createdInstance = createdInstance
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("MATERIAL_ERROR", ex.Message);
            }
        }

        public static RpcResponse ListMaterials(RpcRequest request)
        {
            var req = request.GetParams<ListMaterialsRequest>();

            try
            {
                var guids = AssetDatabase.FindAssets("t:Material", new[] { req.folder });
                var materials = new List<MaterialInfo>();

                foreach (var guid in guids)
                {
                    if (materials.Count >= req.maxResults) break;

                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    
                    if (material == null) continue;

                    // Apply filters
                    if (!string.IsNullOrEmpty(req.nameFilter) && 
                        !material.name.Contains(req.nameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(req.shaderFilter) && material.shader != null &&
                        !material.shader.name.Contains(req.shaderFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    materials.Add(new MaterialInfo
                    {
                        path = path,
                        name = material.name,
                        shaderName = material.shader != null ? material.shader.name : "None",
                        renderQueue = material.renderQueue
                    });
                }

                return RpcResponse.Success(new ListMaterialsResponse
                {
                    totalCount = guids.Length,
                    materials = materials
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("LIST_ERROR", ex.Message);
            }
        }

        public static RpcResponse ListShaders(RpcRequest request)
        {
            try
            {
                var shaders = new List<ShaderInfo>();
                var shaderGuids = AssetDatabase.FindAssets("t:Shader");

                foreach (var guid in shaderGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    
                    if (shader == null) continue;

                    shaders.Add(new ShaderInfo
                    {
                        name = shader.name,
                        propertyCount = ShaderUtil.GetPropertyCount(shader),
                        isSupported = shader.isSupported
                    });
                }

                // Also add built-in shaders that are commonly used
                var builtInShaders = new[] { "Standard", "Standard (Specular setup)", "Unlit/Color", "Unlit/Texture", "UI/Default" };
                foreach (var shaderName in builtInShaders)
                {
                    var shader = Shader.Find(shaderName);
                    if (shader != null && !shaders.Any(s => s.name == shader.name))
                    {
                        shaders.Add(new ShaderInfo
                        {
                            name = shader.name,
                            propertyCount = ShaderUtil.GetPropertyCount(shader),
                            isSupported = shader.isSupported
                        });
                    }
                }

                return RpcResponse.Success(new ListShadersResponse { shaders = shaders });
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("LIST_ERROR", ex.Message);
            }
        }

        #region Helper Methods

        private static string GetPropertyValueString(Material material, string propName, ShaderUtil.ShaderPropertyType propType)
        {
            switch (propType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    var color = material.GetColor(propName);
                    return $"RGBA({color.r:F3}, {color.g:F3}, {color.b:F3}, {color.a:F3})";
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    return material.GetFloat(propName).ToString("F4");
                case ShaderUtil.ShaderPropertyType.Vector:
                    var vec = material.GetVector(propName);
                    return $"({vec.x:F3}, {vec.y:F3}, {vec.z:F3}, {vec.w:F3})";
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    var tex = material.GetTexture(propName);
                    return tex != null ? AssetDatabase.GetAssetPath(tex) : "None";
                default:
                    return "";
            }
        }

        private static Color ParseColor(string value)
        {
            if (value.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(value, out Color c);
                return c;
            }

            // Handle color names
            switch (value.ToLower())
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "white": return Color.white;
                case "black": return Color.black;
                case "yellow": return Color.yellow;
                case "cyan": return Color.cyan;
                case "magenta": return Color.magenta;
                case "gray": case "grey": return Color.gray;
            }

            value = value.Replace("RGBA(", "").Replace("RGB(", "").Replace(")", "");
            var parts = value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            
            if (parts.Length >= 4)
                return new Color(parts[0], parts[1], parts[2], parts[3]);
            if (parts.Length >= 3)
                return new Color(parts[0], parts[1], parts[2], 1f);
            
            return Color.white;
        }

        private static Vector4 ParseVector4(string value)
        {
            value = value.Replace("(", "").Replace(")", "");
            var parts = value.Split(',').Select(s => float.Parse(s.Trim())).ToArray();
            return new Vector4(
                parts.Length > 0 ? parts[0] : 0,
                parts.Length > 1 ? parts[1] : 0,
                parts.Length > 2 ? parts[2] : 0,
                parts.Length > 3 ? parts[3] : 0
            );
        }

        #endregion
    }
}
