// ============================================================================
// UnityVision Bridge - Test Runner Service
// Service for running Unity Test Framework tests via MCP
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Services
{
    /// <summary>
    /// Test result data
    /// </summary>
    public class TestResultData
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; } // Passed, Failed, Skipped, Inconclusive
        public double Duration { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
    }
    
    /// <summary>
    /// Test run summary
    /// </summary>
    public class TestRunSummary
    {
        public int TotalTests { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public double TotalDuration { get; set; }
        public List<TestResultData> Results { get; set; } = new List<TestResultData>();
    }
    
    /// <summary>
    /// Service for running Unity tests
    /// </summary>
    public class TestRunnerService : ICallbacks
    {
        private static TestRunnerService _instance;
        public static TestRunnerService Instance => _instance ?? (_instance = new TestRunnerService());
        
        private TaskCompletionSource<TestRunSummary> _currentRunTcs;
        private TestRunSummary _currentSummary;
        private bool _isRunning;
        
        public bool IsRunning => _isRunning;
        
        public TestRunnerService()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(this);
        }
        
        /// <summary>
        /// Get all available tests
        /// </summary>
        public JArray GetAvailableTests(TestMode mode = TestMode.EditMode | TestMode.PlayMode)
        {
            var tests = new JArray();
            
            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                
                // Get EditMode tests
                if ((mode & TestMode.EditMode) != 0)
                {
                    api.RetrieveTestList(TestMode.EditMode, (testRoot) =>
                    {
                        AddTestsToArray(testRoot, tests, "EditMode");
                    });
                }
                
                // Get PlayMode tests
                if ((mode & TestMode.PlayMode) != 0)
                {
                    api.RetrieveTestList(TestMode.PlayMode, (testRoot) =>
                    {
                        AddTestsToArray(testRoot, tests, "PlayMode");
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log("ERROR", "TestRunnerService", $"Error getting tests: {ex.Message}");
            }
            
            return tests;
        }
        
        private void AddTestsToArray(ITestAdaptor test, JArray array, string mode)
        {
            if (test == null) return;
            
            if (!test.IsSuite)
            {
                array.Add(new JObject
                {
                    ["name"] = test.Name,
                    ["fullName"] = test.FullName,
                    ["mode"] = mode,
                    ["categories"] = new JArray(test.Categories ?? Array.Empty<string>())
                });
            }
            
            if (test.Children != null)
            {
                foreach (var child in test.Children)
                {
                    AddTestsToArray(child, array, mode);
                }
            }
        }
        
        /// <summary>
        /// Run tests with optional filter
        /// </summary>
        public Task<TestRunSummary> RunTestsAsync(TestMode mode = TestMode.EditMode, string filter = null)
        {
            if (_isRunning)
            {
                var busySummary = new TestRunSummary();
                busySummary.Results.Add(new TestResultData { Name = "Error", Status = "Skipped", Message = "Tests already running" });
                return Task.FromResult(busySummary);
            }
            
            _isRunning = true;
            _currentRunTcs = new TaskCompletionSource<TestRunSummary>();
            _currentSummary = new TestRunSummary();
            
            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                
                var filterObj = new Filter
                {
                    testMode = mode
                };
                
                if (!string.IsNullOrEmpty(filter))
                {
                    filterObj.testNames = new[] { filter };
                }
                
                api.Execute(new ExecutionSettings(filterObj));
                
                FileLogger.Log("INFO", "TestRunnerService", $"Started test run (mode: {mode}, filter: {filter ?? "all"})");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _currentRunTcs.SetException(ex);
                FileLogger.Log("ERROR", "TestRunnerService", $"Error starting tests: {ex.Message}");
            }
            
            return _currentRunTcs.Task;
        }
        
        // ICallbacks implementation
        
        public void RunStarted(ITestAdaptor testsToRun)
        {
            _currentSummary = new TestRunSummary();
            FileLogger.Log("INFO", "TestRunnerService", "Test run started");
        }
        
        public void RunFinished(ITestResultAdaptor result)
        {
            _currentSummary.TotalDuration = result.Duration;
            _isRunning = false;
            
            FileLogger.Log("INFO", "TestRunnerService", 
                $"Test run finished: {_currentSummary.Passed} passed, {_currentSummary.Failed} failed, {_currentSummary.Skipped} skipped");
            
            _currentRunTcs?.TrySetResult(_currentSummary);
        }
        
        public void TestStarted(ITestAdaptor test)
        {
            // Optional: track test start
        }
        
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.Test.IsSuite) return;
            
            _currentSummary.TotalTests++;
            
            var testResult = new TestResultData
            {
                Name = result.Test.Name,
                FullName = result.Test.FullName,
                Duration = result.Duration,
                Message = result.Message,
                StackTrace = result.StackTrace
            };
            
            switch (result.TestStatus)
            {
                case TestStatus.Passed:
                    testResult.Status = "Passed";
                    _currentSummary.Passed++;
                    break;
                case TestStatus.Failed:
                    testResult.Status = "Failed";
                    _currentSummary.Failed++;
                    break;
                case TestStatus.Skipped:
                    testResult.Status = "Skipped";
                    _currentSummary.Skipped++;
                    break;
                default:
                    testResult.Status = "Inconclusive";
                    break;
            }
            
            _currentSummary.Results.Add(testResult);
        }
    }
}
