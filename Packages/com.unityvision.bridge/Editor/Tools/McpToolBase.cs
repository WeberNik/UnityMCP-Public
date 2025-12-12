// ============================================================================
// UnityVision Bridge - MCP Tool Base Class
// Base class for custom MCP tools that can be registered dynamically
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace UnityVision.Editor.Tools
{
    /// <summary>
    /// Parameter definition for an MCP tool
    /// </summary>
    [Serializable]
    public class McpToolParameter
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; } = "string"; // string, number, boolean, object, array
        public bool Required { get; set; } = false;
        public object DefaultValue { get; set; }
        
        public JObject ToJson()
        {
            var json = new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["type"] = Type,
                ["required"] = Required
            };
            
            if (DefaultValue != null)
            {
                json["default_value"] = JToken.FromObject(DefaultValue);
            }
            
            return json;
        }
    }
    
    /// <summary>
    /// Base class for MCP Unity tools that interact with the Unity Editor.
    /// Supports both synchronous and asynchronous execution patterns.
    /// </summary>
    public abstract class McpToolBase
    {
        /// <summary>
        /// The unique name of the tool as used in API calls
        /// </summary>
        public abstract string Name { get; }
        
        /// <summary>
        /// Human-readable description of the tool's functionality
        /// </summary>
        public abstract string Description { get; }
        
        /// <summary>
        /// List of parameters this tool accepts
        /// </summary>
        public virtual List<McpToolParameter> Parameters { get; } = new List<McpToolParameter>();
        
        /// <summary>
        /// Whether the tool returns structured output (JSON) vs plain text
        /// </summary>
        public virtual bool StructuredOutput { get; } = true;
        
        /// <summary>
        /// Flag indicating if the tool executes asynchronously.
        /// If true, ExecuteAsync should be overridden.
        /// If false, Execute should be overridden.
        /// </summary>
        public virtual bool IsAsync { get; } = false;
        
        /// <summary>
        /// Whether this tool requires polling for completion
        /// </summary>
        public virtual bool RequiresPolling { get; } = false;
        
        /// <summary>
        /// The action to call for polling status (if RequiresPolling is true)
        /// </summary>
        public virtual string PollAction { get; } = null;
        
        /// <summary>
        /// Execute the tool synchronously with the provided parameters.
        /// Override this for tools that can execute quickly.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <returns>The result of the tool execution as a JObject</returns>
        public virtual JObject Execute(JObject parameters)
        {
            return CreateErrorResponse(
                "Execute must be overridden if IsAsync is false.",
                "implementation_error"
            );
        }
        
        /// <summary>
        /// Execute the tool asynchronously with the provided parameters.
        /// Override this for tools that need to run on the Unity main thread
        /// or perform long-running operations.
        /// </summary>
        /// <param name="parameters">Tool parameters as a JObject</param>
        /// <param name="tcs">TaskCompletionSource to set the result</param>
        public virtual void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            tcs.TrySetException(new NotImplementedException(
                "ExecuteAsync must be overridden if IsAsync is true."
            ));
        }
        
        /// <summary>
        /// Convert this tool definition to JSON for registration
        /// </summary>
        public JObject ToRegistrationJson()
        {
            var json = new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["structured_output"] = StructuredOutput,
                ["requires_polling"] = RequiresPolling
            };
            
            if (!string.IsNullOrEmpty(PollAction))
            {
                json["poll_action"] = PollAction;
            }
            
            var paramsArray = new JArray();
            foreach (var param in Parameters)
            {
                paramsArray.Add(param.ToJson());
            }
            json["parameters"] = paramsArray;
            
            return json;
        }
        
        /// <summary>
        /// Helper to get a required string parameter
        /// </summary>
        protected string GetRequiredString(JObject parameters, string name)
        {
            var value = parameters[name]?.ToString();
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"Required parameter '{name}' is missing or empty");
            }
            return value;
        }
        
        /// <summary>
        /// Helper to get an optional string parameter with default
        /// </summary>
        protected string GetOptionalString(JObject parameters, string name, string defaultValue = null)
        {
            return parameters[name]?.ToString() ?? defaultValue;
        }
        
        /// <summary>
        /// Helper to get an optional int parameter with default
        /// </summary>
        protected int GetOptionalInt(JObject parameters, string name, int defaultValue = 0)
        {
            return parameters[name]?.Value<int>() ?? defaultValue;
        }
        
        /// <summary>
        /// Helper to get an optional bool parameter with default
        /// </summary>
        protected bool GetOptionalBool(JObject parameters, string name, bool defaultValue = false)
        {
            return parameters[name]?.Value<bool>() ?? defaultValue;
        }
        
        /// <summary>
        /// Create a success response
        /// </summary>
        protected static JObject CreateSuccessResponse(object result)
        {
            return new JObject
            {
                ["success"] = true,
                ["result"] = result != null ? JToken.FromObject(result) : null
            };
        }
        
        /// <summary>
        /// Create an error response
        /// </summary>
        public static JObject CreateErrorResponse(string message, string errorType = "error")
        {
            return new JObject
            {
                ["error"] = new JObject
                {
                    ["type"] = errorType,
                    ["message"] = message
                }
            };
        }
    }
}
