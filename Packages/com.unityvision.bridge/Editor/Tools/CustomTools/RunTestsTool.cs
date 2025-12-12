// ============================================================================
// UnityVision Bridge - Run Tests Tool
// Tool for running Unity Test Framework tests
// ============================================================================

using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor.TestTools.TestRunner.Api;
using UnityVision.Editor.Services;

namespace UnityVision.Editor.Tools.CustomTools
{
    /// <summary>
    /// Tool for running Unity tests via MCP
    /// </summary>
    public class RunTestsTool : McpToolBase
    {
        public override string Name => "unity_run_tests";
        
        public override string Description => "Run Unity Test Framework tests (EditMode or PlayMode)";
        
        public override List<McpToolParameter> Parameters => new List<McpToolParameter>
        {
            new McpToolParameter
            {
                Name = "mode",
                Description = "Test mode: 'edit' for EditMode tests, 'play' for PlayMode tests",
                Type = "string",
                Required = false,
                DefaultValue = "edit"
            },
            new McpToolParameter
            {
                Name = "filter",
                Description = "Optional test name filter (full test name or partial match)",
                Type = "string",
                Required = false
            }
        };
        
        public override bool IsAsync => true;
        public override bool RequiresPolling => false;
        
        public override async void ExecuteAsync(JObject parameters, TaskCompletionSource<JObject> tcs)
        {
            try
            {
                var modeStr = GetOptionalString(parameters, "mode", "edit").ToLowerInvariant();
                var filter = GetOptionalString(parameters, "filter", null);
                
                var mode = modeStr == "play" ? TestMode.PlayMode : TestMode.EditMode;
                
                var summary = await TestRunnerService.Instance.RunTestsAsync(mode, filter);
                
                tcs.SetResult(CreateSuccessResponse(new
                {
                    totalTests = summary.TotalTests,
                    passed = summary.Passed,
                    failed = summary.Failed,
                    skipped = summary.Skipped,
                    duration = summary.TotalDuration,
                    results = summary.Results
                }));
            }
            catch (System.Exception ex)
            {
                tcs.SetResult(CreateErrorResponse($"Failed to run tests: {ex.Message}", "test_error"));
            }
        }
    }
    
    /// <summary>
    /// Tool for listing available Unity tests
    /// </summary>
    public class ListTestsTool : McpToolBase
    {
        public override string Name => "unity_list_tests";
        
        public override string Description => "List all available Unity Test Framework tests";
        
        public override List<McpToolParameter> Parameters => new List<McpToolParameter>
        {
            new McpToolParameter
            {
                Name = "mode",
                Description = "Test mode: 'edit', 'play', or 'all'",
                Type = "string",
                Required = false,
                DefaultValue = "all"
            }
        };
        
        public override bool IsAsync => false;
        
        public override JObject Execute(JObject parameters)
        {
            var modeStr = GetOptionalString(parameters, "mode", "all").ToLowerInvariant();
            
            TestMode mode;
            switch (modeStr)
            {
                case "edit":
                    mode = TestMode.EditMode;
                    break;
                case "play":
                    mode = TestMode.PlayMode;
                    break;
                default:
                    mode = TestMode.EditMode | TestMode.PlayMode;
                    break;
            }
            
            var tests = TestRunnerService.Instance.GetAvailableTests(mode);
            
            return CreateSuccessResponse(new
            {
                count = tests.Count,
                tests = tests
            });
        }
    }
}
