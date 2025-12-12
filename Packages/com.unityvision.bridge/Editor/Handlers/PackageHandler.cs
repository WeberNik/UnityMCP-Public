// ============================================================================
// UnityVision Bridge - Package Handler
// Unity Package Manager operations (list, add, remove)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    /// <summary>
    /// Handles Unity Package Manager operations.
    /// </summary>
    public static class PackageHandler
    {
        private static ListRequest _listRequest;
        private static AddRequest _addRequest;
        private static RemoveRequest _removeRequest;

        // ====================================================================
        // RPC Entry Point (for RpcHandler registration)
        // ====================================================================

        public static RpcResponse HandleRpc(RpcRequest request)
        {
            try
            {
                var args = request.Params?.ToObject<Dictionary<string, object>>();
                string action = args?.ContainsKey("action") == true ? args["action"]?.ToString() : null;
                
                if (string.IsNullOrEmpty(action))
                {
                    return RpcResponse.Failure("INVALID_PARAMS", "action is required");
                }

                var result = Handle(action, args);
                return RpcResponse.Success(result);
            }
            catch (Exception ex)
            {
                return RpcResponse.Failure("PACKAGE_ERROR", ex.Message);
            }
        }

        // ====================================================================
        // Main Entry Point
        // ====================================================================

        public static object Handle(string action, object parameters)
        {
            var args = parameters as Dictionary<string, object>;
            if (args == null)
            {
                args = new Dictionary<string, object>();
            }

            switch (action?.ToLower())
            {
                case "list":
                    return ListPackages(args);
                case "add":
                    return AddPackage(args);
                case "remove":
                    return RemovePackage(args);
                default:
                    return new { error = $"Unknown action: {action}" };
            }
        }

        // ====================================================================
        // List Packages
        // ====================================================================

        private static object ListPackages(Dictionary<string, object> args)
        {
            try
            {
                bool includeBuiltIn = args.ContainsKey("includeBuiltIn") && 
                    args["includeBuiltIn"] is bool b && b;

                _listRequest = Client.List(includeBuiltIn);
                
                // Wait for the request to complete using a spin-wait that yields to Unity
                // This is more Unity-friendly than Thread.Sleep
                int timeout = 30000; // 30 seconds
                var startTime = DateTime.Now;
                
                while (!_listRequest.IsCompleted)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
                    {
                        return new { error = "Package list request timed out" };
                    }
                    // Yield to allow Unity to process
                    System.Threading.Thread.Yield();
                }

                if (_listRequest.Status == StatusCode.Failure)
                {
                    return new { error = $"Failed to list packages: {_listRequest.Error?.message}" };
                }

                var packages = new List<object>();
                foreach (var package in _listRequest.Result)
                {
                    packages.Add(new
                    {
                        name = package.name,
                        displayName = package.displayName,
                        version = package.version,
                        description = package.description,
                        source = package.source.ToString(),
                        isDirectDependency = package.isDirectDependency,
                        documentationUrl = package.documentationUrl,
                        changelogUrl = package.changelogUrl,
                        licensesUrl = package.licensesUrl
                    });
                }

                return new
                {
                    success = true,
                    count = packages.Count,
                    packages = packages
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to list packages: {ex.Message}" };
            }
        }

        // ====================================================================
        // Add Package
        // ====================================================================

        private static object AddPackage(Dictionary<string, object> args)
        {
            try
            {
                string packageId = null;

                // Check for git URL first
                if (args.ContainsKey("gitUrl") && args["gitUrl"] != null)
                {
                    packageId = args["gitUrl"].ToString();
                }
                else if (args.ContainsKey("packageName") && args["packageName"] != null)
                {
                    packageId = args["packageName"].ToString();
                    
                    // Add version if specified
                    if (args.ContainsKey("version") && args["version"] != null)
                    {
                        packageId += "@" + args["version"].ToString();
                    }
                }
                else
                {
                    return new { error = "Either packageName or gitUrl is required" };
                }

                _addRequest = Client.Add(packageId);
                
                // Wait for the request to complete using spin-wait
                int timeout = 120000; // 2 minutes for package installation
                var startTime = DateTime.Now;
                
                while (!_addRequest.IsCompleted)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
                    {
                        return new { error = "Package add request timed out" };
                    }
                    System.Threading.Thread.Yield();
                }

                if (_addRequest.Status == StatusCode.Failure)
                {
                    return new { error = $"Failed to add package: {_addRequest.Error?.message}" };
                }

                var package = _addRequest.Result;
                return new
                {
                    success = true,
                    message = $"Successfully installed {package.displayName}",
                    package = new
                    {
                        name = package.name,
                        displayName = package.displayName,
                        version = package.version,
                        description = package.description
                    }
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to add package: {ex.Message}" };
            }
        }

        // ====================================================================
        // Remove Package
        // ====================================================================

        private static object RemovePackage(Dictionary<string, object> args)
        {
            try
            {
                if (!args.ContainsKey("packageName") || args["packageName"] == null)
                {
                    return new { error = "packageName is required" };
                }

                string packageName = args["packageName"].ToString();

                _removeRequest = Client.Remove(packageName);
                
                // Wait for the request to complete using spin-wait
                int timeout = 60000; // 1 minute
                var startTime = DateTime.Now;
                
                while (!_removeRequest.IsCompleted)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
                    {
                        return new { error = "Package remove request timed out" };
                    }
                    System.Threading.Thread.Yield();
                }

                if (_removeRequest.Status == StatusCode.Failure)
                {
                    return new { error = $"Failed to remove package: {_removeRequest.Error?.message}" };
                }

                return new
                {
                    success = true,
                    message = $"Successfully removed {packageName}",
                    packageName = packageName
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to remove package: {ex.Message}" };
            }
        }
    }
}
