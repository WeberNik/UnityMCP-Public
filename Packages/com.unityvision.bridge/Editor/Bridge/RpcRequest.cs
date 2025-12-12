// ============================================================================
// UnityVision Bridge - RPC Request/Response Types
// ============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityVision.Editor.Bridge
{
    /// <summary>
    /// Incoming RPC request from the MCP server
    /// </summary>
    [Serializable]
    public class RpcRequest
    {
        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JObject Params { get; set; }

        public T GetParams<T>() where T : class, new()
        {
            if (Params == null) return new T();
            return Params.ToObject<T>();
        }
    }

    /// <summary>
    /// Outgoing RPC response to the MCP server
    /// </summary>
    [Serializable]
    public class RpcResponse
    {
        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public RpcError Error { get; set; }

        public static RpcResponse Success(object result)
        {
            return new RpcResponse { Result = result };
        }

        public static RpcResponse Failure(string code, string message, object details = null)
        {
            return new RpcResponse
            {
                Error = new RpcError
                {
                    Code = code,
                    Message = message,
                    Details = details
                }
            };
        }
    }

    /// <summary>
    /// Error information in an RPC response
    /// </summary>
    [Serializable]
    public class RpcError
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("details", NullValueHandling = NullValueHandling.Ignore)]
        public object Details { get; set; }
    }

    // =========================================================================
    // Common Data Types
    // =========================================================================

    [Serializable]
    public class Vector3Data
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }

        public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(X, Y, Z);

        public static Vector3Data FromVector3(UnityEngine.Vector3 v) =>
            new Vector3Data { X = v.x, Y = v.y, Z = v.z };
    }

    [Serializable]
    public class Vector2Data
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }

        public UnityEngine.Vector2 ToVector2() => new UnityEngine.Vector2(X, Y);

        public static Vector2Data FromVector2(UnityEngine.Vector2 v) =>
            new Vector2Data { X = v.x, Y = v.y };
    }

    [Serializable]
    public class TransformData
    {
        [JsonProperty("position")] public Vector3Data Position { get; set; }
        [JsonProperty("rotation")] public Vector3Data Rotation { get; set; }
        [JsonProperty("scale")] public Vector3Data Scale { get; set; }
    }

    [Serializable]
    public class ComponentSpec
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("properties")] public Dictionary<string, object> Properties { get; set; }
    }
}
