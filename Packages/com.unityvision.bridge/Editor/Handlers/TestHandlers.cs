// ============================================================================
// UnityVision Bridge - Test Handlers
// Handlers for running Unity tests
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class TestHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class RunTestsRequest
        {
            public string testMode = "EditMode"; // EditMode, PlayMode, Both
            public string filter = "";
            public int timeout = 300;
        }

        [Serializable]
        public class TestResult
        {
            public string name;
            public bool passed;
            public string message;
            public string stackTrace;
            public float duration;
        }

        [Serializable]
        public class TestSummary
        {
            public int total;
            public int passed;
            public int failed;
            public int ignored;
            public float duration;
        }

        [Serializable]
        public class RunTestsResponse
        {
            public bool success;
            public TestSummary summary;
            public List<TestResult> failedTests;
        }

        #endregion

        private static TestRunnerApi _testRunnerApi;
        private static RunTestsResponse _lastResult;
        private static bool _testRunComplete;
        private static DateTime _testStartTime;

        public static RpcResponse RunTests(RpcRequest request)
        {
            var req = request.GetParams<RunTestsRequest>();

            try
            {
                _testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();
                _lastResult = new RunTestsResponse
                {
                    success = true,
                    summary = new TestSummary(),
                    failedTests = new List<TestResult>()
                };
                _testRunComplete = false;
                _testStartTime = DateTime.Now;

                // Create callbacks
                var callbacks = new TestCallbacks();
                _testRunnerApi.RegisterCallbacks(callbacks);

                // Build filter
                var filter = new Filter();

                // Set test mode
                switch (req.testMode.ToLowerInvariant())
                {
                    case "editmode":
                        filter.testMode = TestMode.EditMode;
                        break;
                    case "playmode":
                        filter.testMode = TestMode.PlayMode;
                        break;
                    case "both":
                        filter.testMode = TestMode.EditMode | TestMode.PlayMode;
                        break;
                    default:
                        return RpcResponse.Failure("INVALID_PARAMS", $"Invalid testMode: {req.testMode}");
                }

                // Set name filter
                if (!string.IsNullOrEmpty(req.filter))
                {
                    filter.testNames = new[] { req.filter };
                }

                // Execute tests
                _testRunnerApi.Execute(new ExecutionSettings(filter));

                // Wait for completion (with timeout)
                var timeout = TimeSpan.FromSeconds(req.timeout);
                while (!_testRunComplete && (DateTime.Now - _testStartTime) < timeout)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (!_testRunComplete)
                {
                    return RpcResponse.Failure("TIMEOUT", $"Test run timed out after {req.timeout} seconds");
                }

                _lastResult.summary.duration = (float)(DateTime.Now - _testStartTime).TotalSeconds;
                return RpcResponse.Success(_lastResult);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("TEST_ERROR", $"Failed to run tests: {ex.Message}");
            }
        }

        private class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                _lastResult.summary.total = CountTests(testsToRun);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                _testRunComplete = true;
            }

            public void TestStarted(ITestAdaptor test)
            {
                // Optional: track individual test starts
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    switch (result.TestStatus)
                    {
                        case TestStatus.Passed:
                            _lastResult.summary.passed++;
                            break;
                        case TestStatus.Failed:
                            _lastResult.summary.failed++;
                            _lastResult.failedTests.Add(new TestResult
                            {
                                name = result.Test.Name,
                                passed = false,
                                message = result.Message,
                                stackTrace = result.StackTrace,
                                duration = (float)result.Duration
                            });
                            break;
                        case TestStatus.Skipped:
                            _lastResult.summary.ignored++;
                            break;
                    }
                }
            }

            private int CountTests(ITestAdaptor test)
            {
                if (!test.HasChildren)
                {
                    return test.IsSuite ? 0 : 1;
                }

                int count = 0;
                foreach (var child in test.Children)
                {
                    count += CountTests(child);
                }
                return count;
            }
        }
    }
}
