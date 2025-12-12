// ============================================================================
// UnityVision Bridge - Batch Operation Handlers
// Execute multiple operations in a single request for performance
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class BatchHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class BatchOperation
        {
            public string id;  // Optional ID for tracking
            public string method;
            public Dictionary<string, object> @params;
        }

        [Serializable]
        public class BatchExecuteRequest
        {
            public List<BatchOperation> operations;
            public bool stopOnError = false;
            public bool atomic = false;  // If true, rollback all on any error (uses Undo)
        }

        [Serializable]
        public class BatchOperationResult
        {
            public string id;
            public string method;
            public bool success;
            public object result;
            public string error;
            public float executionTimeMs;
        }

        [Serializable]
        public class BatchExecuteResponse
        {
            public bool success;
            public int totalOperations;
            public int successCount;
            public int failureCount;
            public List<BatchOperationResult> results;
            public float totalExecutionTimeMs;
        }

        #endregion

        public static RpcResponse BatchExecute(RpcRequest request)
        {
            var req = request.GetParams<BatchExecuteRequest>();

            if (req.operations == null || req.operations.Count == 0)
            {
                return RpcResponse.Failure("INVALID_PARAMS", "operations array is required and must not be empty");
            }

            var startTime = DateTime.Now;
            var results = new List<BatchOperationResult>();
            int successCount = 0;
            int failureCount = 0;
            int undoGroup = -1;

            // Start undo group if atomic
            if (req.atomic)
            {
                UnityEditor.Undo.IncrementCurrentGroup();
                undoGroup = UnityEditor.Undo.GetCurrentGroup();
                UnityEditor.Undo.SetCurrentGroupName("Batch Operation");
            }

            try
            {
                foreach (var op in req.operations)
                {
                    var opStartTime = DateTime.Now;
                    var result = new BatchOperationResult
                    {
                        id = op.id ?? Guid.NewGuid().ToString(),
                        method = op.method
                    };

                    try
                    {
                        // Create a synthetic RpcRequest for the operation
                        var innerRequest = new RpcRequest
                        {
                            Method = op.method,
                            Params = op.@params != null 
                                ? Newtonsoft.Json.Linq.JObject.FromObject(op.@params) 
                                : new Newtonsoft.Json.Linq.JObject()
                        };

                        // Execute the operation
                        var response = RpcHandler.HandleRequest(innerRequest);

                        if (response.Error != null)
                        {
                            result.success = false;
                            result.error = response.Error.Message;
                            failureCount++;

                            if (req.stopOnError)
                            {
                                result.executionTimeMs = (float)(DateTime.Now - opStartTime).TotalMilliseconds;
                                results.Add(result);
                                break;
                            }
                        }
                        else
                        {
                            result.success = true;
                            result.result = response.Result;
                            successCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.success = false;
                        result.error = ex.Message;
                        failureCount++;

                        if (req.stopOnError)
                        {
                            result.executionTimeMs = (float)(DateTime.Now - opStartTime).TotalMilliseconds;
                            results.Add(result);
                            break;
                        }
                    }

                    result.executionTimeMs = (float)(DateTime.Now - opStartTime).TotalMilliseconds;
                    results.Add(result);
                }

                // If atomic and any failures, rollback
                if (req.atomic && failureCount > 0 && undoGroup >= 0)
                {
                    UnityEditor.Undo.RevertAllDownToGroup(undoGroup);
                    Debug.Log($"[UnityVision] Batch operation rolled back due to {failureCount} failures");
                }
            }
            catch (Exception ex)
            {
                // Rollback on unexpected error if atomic
                if (req.atomic && undoGroup >= 0)
                {
                    UnityEditor.Undo.RevertAllDownToGroup(undoGroup);
                }

                return RpcResponse.Failure("BATCH_ERROR", $"Batch execution failed: {ex.Message}");
            }

            var totalTime = (float)(DateTime.Now - startTime).TotalMilliseconds;

            return RpcResponse.Success(new BatchExecuteResponse
            {
                success = failureCount == 0,
                totalOperations = req.operations.Count,
                successCount = successCount,
                failureCount = failureCount,
                results = results,
                totalExecutionTimeMs = totalTime
            });
        }
    }
}
