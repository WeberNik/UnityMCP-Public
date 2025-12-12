// ============================================================================
// UnityVision Bridge - Tool Registry
// Manages registration and discovery of custom MCP tools
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Tools
{
    /// <summary>
    /// Registry for managing custom MCP tools.
    /// Supports automatic discovery and manual registration.
    /// </summary>
    public static class ToolRegistry
    {
        private static readonly Dictionary<string, McpToolBase> _tools = new Dictionary<string, McpToolBase>();
        private static bool _initialized = false;
        
        /// <summary>
        /// Get all registered tools
        /// </summary>
        public static IReadOnlyDictionary<string, McpToolBase> Tools => _tools;
        
        /// <summary>
        /// Number of registered tools
        /// </summary>
        public static int Count => _tools.Count;
        
        /// <summary>
        /// Initialize the registry and discover tools
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            _tools.Clear();
            DiscoverTools();
            _initialized = true;
            
            FileLogger.Log("INFO", "ToolRegistry", $"Initialized with {_tools.Count} custom tools");
        }
        
        /// <summary>
        /// Discover all tools that inherit from McpToolBase
        /// </summary>
        private static void DiscoverTools()
        {
            try
            {
                // Find all types that inherit from McpToolBase
                var toolTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly =>
                    {
                        try
                        {
                            return assembly.GetTypes();
                        }
                        catch
                        {
                            return Array.Empty<Type>();
                        }
                    })
                    .Where(type => 
                        type.IsClass && 
                        !type.IsAbstract && 
                        typeof(McpToolBase).IsAssignableFrom(type))
                    .ToList();
                
                foreach (var toolType in toolTypes)
                {
                    try
                    {
                        var tool = (McpToolBase)Activator.CreateInstance(toolType);
                        RegisterTool(tool);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.Log("WARN", "ToolRegistry", $"Failed to instantiate tool {toolType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log("ERROR", "ToolRegistry", $"Error discovering tools: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Register a tool manually
        /// </summary>
        public static void RegisterTool(McpToolBase tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }
            
            if (string.IsNullOrEmpty(tool.Name))
            {
                throw new ArgumentException("Tool name cannot be empty");
            }
            
            if (_tools.ContainsKey(tool.Name))
            {
                FileLogger.Log("WARN", "ToolRegistry", $"Tool '{tool.Name}' already registered, replacing...");
            }
            
            _tools[tool.Name] = tool;
            FileLogger.Log("INFO", "ToolRegistry", $"Registered tool: {tool.Name}");
        }
        
        /// <summary>
        /// Unregister a tool by name
        /// </summary>
        public static bool UnregisterTool(string name)
        {
            if (_tools.Remove(name))
            {
                FileLogger.Log("INFO", "ToolRegistry", $"Unregistered tool: {name}");
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Try to get a tool by name
        /// </summary>
        public static bool TryGetTool(string name, out McpToolBase tool)
        {
            return _tools.TryGetValue(name, out tool);
        }
        
        /// <summary>
        /// Check if a tool exists
        /// </summary>
        public static bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }
        
        /// <summary>
        /// Execute a tool by name
        /// </summary>
        public static async Task<JObject> ExecuteToolAsync(string name, JObject parameters)
        {
            if (!TryGetTool(name, out var tool))
            {
                return McpToolBase.CreateErrorResponse($"Unknown tool: {name}", "unknown_tool");
            }
            
            try
            {
                if (tool.IsAsync)
                {
                    var tcs = new TaskCompletionSource<JObject>();
                    tool.ExecuteAsync(parameters, tcs);
                    return await tcs.Task;
                }
                else
                {
                    return tool.Execute(parameters);
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log("ERROR", "ToolRegistry", $"Error executing tool {name}: {ex.Message}");
                return McpToolBase.CreateErrorResponse($"Tool execution failed: {ex.Message}", "execution_error");
            }
        }
        
        /// <summary>
        /// Get all tool definitions as JSON array for registration
        /// </summary>
        public static JArray GetToolDefinitionsJson()
        {
            var array = new JArray();
            foreach (var tool in _tools.Values)
            {
                array.Add(tool.ToRegistrationJson());
            }
            return array;
        }
        
        /// <summary>
        /// Clear all registered tools
        /// </summary>
        public static void Clear()
        {
            _tools.Clear();
            _initialized = false;
            FileLogger.Log("INFO", "ToolRegistry", "Cleared all tools");
        }
    }
}
