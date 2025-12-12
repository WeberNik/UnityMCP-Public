// ============================================================================
// UnityVision Bridge - Unity JSON Converters
// Custom JSON converters for Unity types
// ============================================================================

using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityVision.Editor.Serialization
{
    /// <summary>
    /// JSON converter for Vector2
    /// Supports: [x, y] array format and {x, y} object format
    /// </summary>
    public class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 ReadJson(JsonReader reader, Type objectType, Vector2 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector2.zero;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                return new Vector2(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f
                );
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                return new Vector2(
                    obj["x"]?.Value<float>() ?? 0f,
                    obj["y"]?.Value<float>() ?? 0f
                );
            }
            
            return Vector2.zero;
        }

        public override void WriteJson(JsonWriter writer, Vector2 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Vector3
    /// Supports: [x, y, z] array format and {x, y, z} object format
    /// </summary>
    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector3.zero;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                return new Vector3(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f,
                    arr.Count > 2 ? arr[2].Value<float>() : 0f
                );
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                return new Vector3(
                    obj["x"]?.Value<float>() ?? 0f,
                    obj["y"]?.Value<float>() ?? 0f,
                    obj["z"]?.Value<float>() ?? 0f
                );
            }
            
            return Vector3.zero;
        }

        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Vector4
    /// </summary>
    public class Vector4Converter : JsonConverter<Vector4>
    {
        public override Vector4 ReadJson(JsonReader reader, Type objectType, Vector4 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector4.zero;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                return new Vector4(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f,
                    arr.Count > 2 ? arr[2].Value<float>() : 0f,
                    arr.Count > 3 ? arr[3].Value<float>() : 0f
                );
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                return new Vector4(
                    obj["x"]?.Value<float>() ?? 0f,
                    obj["y"]?.Value<float>() ?? 0f,
                    obj["z"]?.Value<float>() ?? 0f,
                    obj["w"]?.Value<float>() ?? 0f
                );
            }
            
            return Vector4.zero;
        }

        public override void WriteJson(JsonWriter writer, Vector4 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WritePropertyName("w");
            writer.WriteValue(value.w);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Quaternion
    /// Supports: [x, y, z, w] array, {x, y, z, w} object, and {euler: [x, y, z]} format
    /// </summary>
    public class QuaternionConverter : JsonConverter<Quaternion>
    {
        public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Quaternion.identity;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                if (arr.Count == 3)
                {
                    // Euler angles
                    return Quaternion.Euler(
                        arr[0].Value<float>(),
                        arr[1].Value<float>(),
                        arr[2].Value<float>()
                    );
                }
                else if (arr.Count >= 4)
                {
                    // Quaternion components
                    return new Quaternion(
                        arr[0].Value<float>(),
                        arr[1].Value<float>(),
                        arr[2].Value<float>(),
                        arr[3].Value<float>()
                    );
                }
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                
                // Check for euler format
                if (obj["euler"] != null)
                {
                    var euler = obj["euler"];
                    if (euler.Type == JTokenType.Array)
                    {
                        var arr = (JArray)euler;
                        return Quaternion.Euler(
                            arr.Count > 0 ? arr[0].Value<float>() : 0f,
                            arr.Count > 1 ? arr[1].Value<float>() : 0f,
                            arr.Count > 2 ? arr[2].Value<float>() : 0f
                        );
                    }
                    else if (euler.Type == JTokenType.Object)
                    {
                        var eulerObj = (JObject)euler;
                        return Quaternion.Euler(
                            eulerObj["x"]?.Value<float>() ?? 0f,
                            eulerObj["y"]?.Value<float>() ?? 0f,
                            eulerObj["z"]?.Value<float>() ?? 0f
                        );
                    }
                }
                
                // Quaternion components
                return new Quaternion(
                    obj["x"]?.Value<float>() ?? 0f,
                    obj["y"]?.Value<float>() ?? 0f,
                    obj["z"]?.Value<float>() ?? 0f,
                    obj["w"]?.Value<float>() ?? 1f
                );
            }
            
            return Quaternion.identity;
        }

        public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WritePropertyName("w");
            writer.WriteValue(value.w);
            writer.WritePropertyName("euler");
            var euler = value.eulerAngles;
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(euler.x);
            writer.WritePropertyName("y");
            writer.WriteValue(euler.y);
            writer.WritePropertyName("z");
            writer.WriteValue(euler.z);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Color
    /// Supports: [r, g, b, a] array, {r, g, b, a} object, and "#RRGGBBAA" hex string
    /// </summary>
    public class ColorConverter : JsonConverter<Color>
    {
        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Color.white;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.String)
            {
                var str = token.Value<string>();
                if (ColorUtility.TryParseHtmlString(str, out Color color))
                    return color;
                return Color.white;
            }
            else if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                return new Color(
                    arr.Count > 0 ? arr[0].Value<float>() : 1f,
                    arr.Count > 1 ? arr[1].Value<float>() : 1f,
                    arr.Count > 2 ? arr[2].Value<float>() : 1f,
                    arr.Count > 3 ? arr[3].Value<float>() : 1f
                );
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                return new Color(
                    obj["r"]?.Value<float>() ?? 1f,
                    obj["g"]?.Value<float>() ?? 1f,
                    obj["b"]?.Value<float>() ?? 1f,
                    obj["a"]?.Value<float>() ?? 1f
                );
            }
            
            return Color.white;
        }

        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(value.r);
            writer.WritePropertyName("g");
            writer.WriteValue(value.g);
            writer.WritePropertyName("b");
            writer.WriteValue(value.b);
            writer.WritePropertyName("a");
            writer.WriteValue(value.a);
            writer.WritePropertyName("hex");
            writer.WriteValue("#" + ColorUtility.ToHtmlStringRGBA(value));
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Rect
    /// </summary>
    public class RectConverter : JsonConverter<Rect>
    {
        public override Rect ReadJson(JsonReader reader, Type objectType, Rect existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Rect.zero;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Array)
            {
                var arr = (JArray)token;
                return new Rect(
                    arr.Count > 0 ? arr[0].Value<float>() : 0f,
                    arr.Count > 1 ? arr[1].Value<float>() : 0f,
                    arr.Count > 2 ? arr[2].Value<float>() : 0f,
                    arr.Count > 3 ? arr[3].Value<float>() : 0f
                );
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                return new Rect(
                    obj["x"]?.Value<float>() ?? 0f,
                    obj["y"]?.Value<float>() ?? 0f,
                    obj["width"]?.Value<float>() ?? 0f,
                    obj["height"]?.Value<float>() ?? 0f
                );
            }
            
            return Rect.zero;
        }

        public override void WriteJson(JsonWriter writer, Rect value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("width");
            writer.WriteValue(value.width);
            writer.WritePropertyName("height");
            writer.WriteValue(value.height);
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Bounds
    /// </summary>
    public class BoundsConverter : JsonConverter<Bounds>
    {
        public override Bounds ReadJson(JsonReader reader, Type objectType, Bounds existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return new Bounds();

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                var center = Vector3.zero;
                var size = Vector3.zero;
                
                if (obj["center"] != null)
                {
                    var c = obj["center"];
                    if (c.Type == JTokenType.Object)
                    {
                        center = new Vector3(
                            c["x"]?.Value<float>() ?? 0f,
                            c["y"]?.Value<float>() ?? 0f,
                            c["z"]?.Value<float>() ?? 0f
                        );
                    }
                }
                
                if (obj["size"] != null)
                {
                    var s = obj["size"];
                    if (s.Type == JTokenType.Object)
                    {
                        size = new Vector3(
                            s["x"]?.Value<float>() ?? 0f,
                            s["y"]?.Value<float>() ?? 0f,
                            s["z"]?.Value<float>() ?? 0f
                        );
                    }
                }
                
                return new Bounds(center, size);
            }
            
            return new Bounds();
        }

        public override void WriteJson(JsonWriter writer, Bounds value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("center");
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.center.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.center.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.center.z);
            writer.WriteEndObject();
            writer.WritePropertyName("size");
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.size.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.size.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.size.z);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
    }

    /// <summary>
    /// JSON converter for Unity Object references
    /// Serializes as path/GUID, deserializes by finding the object
    /// </summary>
    public class UnityObjectConverter : JsonConverter<UnityEngine.Object>
    {
        public override UnityEngine.Object ReadJson(JsonReader reader, Type objectType, UnityEngine.Object existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.String)
            {
                var str = token.Value<string>();
                
                // Try as asset path
                if (str.StartsWith("Assets/"))
                {
                    return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(str);
                }
                
                // Try as GUID
                if (str.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(str, "^[a-fA-F0-9]+$"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(str);
                    if (!string.IsNullOrEmpty(path))
                    {
                        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    }
                }
                
                // Try as GameObject path in scene
                var go = GameObject.Find(str);
                if (go != null) return go;
            }
            else if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                
                // Try path first
                var path = obj["path"]?.Value<string>();
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith("Assets/"))
                    {
                        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    }
                    else
                    {
                        var go = GameObject.Find(path);
                        if (go != null) return go;
                    }
                }
                
                // Try GUID
                var guid = obj["guid"]?.Value<string>();
                if (!string.IsNullOrEmpty(guid))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    }
                }
                
                // Try instanceId
                var instanceId = obj["instanceId"]?.Value<int>();
                if (instanceId.HasValue && instanceId.Value != 0)
                {
                    return EditorUtility.InstanceIDToObject(instanceId.Value);
                }
            }
            else if (token.Type == JTokenType.Integer)
            {
                // Instance ID
                var instanceId = token.Value<int>();
                if (instanceId != 0)
                {
                    return EditorUtility.InstanceIDToObject(instanceId);
                }
            }
            
            return null;
        }

        public override void WriteJson(JsonWriter writer, UnityEngine.Object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName("name");
            writer.WriteValue(value.name);
            writer.WritePropertyName("type");
            writer.WriteValue(value.GetType().Name);
            writer.WritePropertyName("instanceId");
            writer.WriteValue(value.GetInstanceID());
            
            // Add asset path and GUID if it's an asset
            var assetPath = AssetDatabase.GetAssetPath(value);
            if (!string.IsNullOrEmpty(assetPath))
            {
                writer.WritePropertyName("path");
                writer.WriteValue(assetPath);
                writer.WritePropertyName("guid");
                writer.WriteValue(AssetDatabase.AssetPathToGUID(assetPath));
            }
            else if (value is GameObject go)
            {
                // Scene object - write hierarchy path
                writer.WritePropertyName("path");
                writer.WriteValue(GetGameObjectPath(go));
            }
            else if (value is Component comp)
            {
                writer.WritePropertyName("path");
                writer.WriteValue(GetGameObjectPath(comp.gameObject));
                writer.WritePropertyName("componentType");
                writer.WriteValue(comp.GetType().Name);
            }
            
            writer.WriteEndObject();
        }
        
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
    }

    /// <summary>
    /// Static class providing configured JsonSerializer with all Unity converters
    /// </summary>
    public static class UnityJsonSerializer
    {
        private static JsonSerializerSettings _settings;
        private static JsonSerializer _serializer;

        public static JsonSerializerSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new JsonSerializerSettings
                    {
                        Converters = new System.Collections.Generic.List<JsonConverter>
                        {
                            new Vector2Converter(),
                            new Vector3Converter(),
                            new Vector4Converter(),
                            new QuaternionConverter(),
                            new ColorConverter(),
                            new RectConverter(),
                            new BoundsConverter(),
                            new UnityObjectConverter()
                        },
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        NullValueHandling = NullValueHandling.Include,
                        Culture = CultureInfo.InvariantCulture
                    };
                }
                return _settings;
            }
        }

        public static JsonSerializer Serializer
        {
            get
            {
                if (_serializer == null)
                {
                    _serializer = JsonSerializer.Create(Settings);
                }
                return _serializer;
            }
        }

        /// <summary>
        /// Serialize an object to JSON string with Unity type support
        /// </summary>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Settings);
        }

        /// <summary>
        /// Deserialize JSON string to object with Unity type support
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
    }
}
