// ============================================================================
// UnityVision Bridge - Ping Tool (Example Custom Tool)
// Simple tool to test custom tool registration
// ============================================================================

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityVision.Editor.Tools.CustomTools
{
    /// <summary>
    /// Simple ping tool for testing custom tool registration
    /// </summary>
    public class PingTool : McpToolBase
    {
        public override string Name => "unity_ping";
        
        public override string Description => "Simple ping tool to test connection and custom tool registration";
        
        public override List<McpToolParameter> Parameters => new List<McpToolParameter>
        {
            new McpToolParameter
            {
                Name = "message",
                Description = "Optional message to echo back",
                Type = "string",
                Required = false,
                DefaultValue = "pong"
            }
        };
        
        public override bool IsAsync => false;
        
        public override JObject Execute(JObject parameters)
        {
            var message = GetOptionalString(parameters, "message", "pong");
            
            return CreateSuccessResponse(new
            {
                response = message,
                timestamp = System.DateTime.Now.ToString("O"),
                unityVersion = Application.unityVersion,
                projectName = Application.productName
            });
        }
    }
}
