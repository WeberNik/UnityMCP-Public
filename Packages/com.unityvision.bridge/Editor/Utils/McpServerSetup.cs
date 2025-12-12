// ============================================================================
// UnityVision Bridge - MCP Server Setup Utility
// Auto-installs and builds the MCP Node.js server
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Utils
{
    /// <summary>
    /// Utility for setting up the MCP Node.js server
    /// </summary>
    public static class McpServerSetup
    {
        private const string MCP_PATH_PREF_KEY = "UnityVision_McpServerPath";
        
        /// <summary>
        /// Get the configured MCP server path
        /// </summary>
        public static string GetServerPath()
        {
            return EditorPrefs.GetString(MCP_PATH_PREF_KEY, "");
        }
        
        /// <summary>
        /// Set the MCP server path
        /// </summary>
        public static void SetServerPath(string path)
        {
            EditorPrefs.SetString(MCP_PATH_PREF_KEY, path);
        }
        
        /// <summary>
        /// Check if the MCP server is properly installed
        /// </summary>
        public static bool IsServerInstalled()
        {
            var serverPath = GetServerPath();
            if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
            {
                return false;
            }
            
            var nodeModulesPath = Path.Combine(serverPath, "node_modules");
            var distPath = Path.Combine(serverPath, "dist");
            
            return Directory.Exists(nodeModulesPath) && Directory.Exists(distPath);
        }
        
        /// <summary>
        /// Check if npm is available
        /// </summary>
        public static bool IsNpmAvailable()
        {
            try
            {
                var result = RunCommand("npm", "--version", null, 5000);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Install the MCP server (npm install + npm run build)
        /// </summary>
        public static bool InstallServer(string serverPath = null)
        {
            serverPath = serverPath ?? GetServerPath();
            
            if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
            {
                UnityEngine.Debug.LogError("[UnityVision] MCP server path not configured or invalid");
                return false;
            }
            
            if (!IsNpmAvailable())
            {
                UnityEngine.Debug.LogError("[UnityVision] npm is not available. Please install Node.js.");
                return false;
            }
            
            FileLogger.Log("INFO", "McpServerSetup", $"Installing MCP server at: {serverPath}");
            
            // Check if node_modules exists
            var nodeModulesPath = Path.Combine(serverPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                UnityEngine.Debug.Log("[UnityVision] Running npm install...");
                var installResult = RunNpmCommand("install", serverPath);
                if (!installResult)
                {
                    UnityEngine.Debug.LogError("[UnityVision] npm install failed");
                    return false;
                }
            }
            
            // Check if dist exists
            var distPath = Path.Combine(serverPath, "dist");
            if (!Directory.Exists(distPath))
            {
                UnityEngine.Debug.Log("[UnityVision] Running npm run build...");
                var buildResult = RunNpmCommand("run build", serverPath);
                if (!buildResult)
                {
                    UnityEngine.Debug.LogError("[UnityVision] npm run build failed");
                    return false;
                }
            }
            
            UnityEngine.Debug.Log("[UnityVision] MCP server installed successfully");
            FileLogger.Log("INFO", "McpServerSetup", "MCP server installed successfully");
            return true;
        }
        
        /// <summary>
        /// Run an npm command
        /// </summary>
        public static bool RunNpmCommand(string arguments, string workingDirectory)
        {
            try
            {
                var result = RunCommand("npm", arguments, workingDirectory, 120000); // 2 minute timeout
                
                if (result.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[UnityVision] npm {arguments} failed:\n{result.Error}");
                    return false;
                }
                
                FileLogger.Log("INFO", "McpServerSetup", $"npm {arguments} completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UnityVision] Error running npm {arguments}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Run a command and return the result
        /// </summary>
        private static CommandResult RunCommand(string command, string arguments, string workingDirectory, int timeoutMs = 30000)
        {
            var result = new CommandResult();
            
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory ?? "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            // On Windows, use cmd.exe for npm
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c {command} {arguments}";
            }
            
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                
                result.Output = process.StandardOutput.ReadToEnd();
                result.Error = process.StandardError.ReadToEnd();
                
                if (!process.WaitForExit(timeoutMs))
                {
                    process.Kill();
                    result.ExitCode = -1;
                    result.Error = "Process timed out";
                }
                else
                {
                    result.ExitCode = process.ExitCode;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Command execution result
        /// </summary>
        public class CommandResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
        
        /// <summary>
        /// Menu item to install/update MCP server
        /// </summary>
        [MenuItem("Window/UnityVision/Install MCP Server")]
        public static void InstallServerMenuItem()
        {
            var serverPath = GetServerPath();
            
            if (string.IsNullOrEmpty(serverPath))
            {
                serverPath = EditorUtility.OpenFolderPanel("Select MCP Server Directory", "", "");
                if (string.IsNullOrEmpty(serverPath))
                {
                    return;
                }
                SetServerPath(serverPath);
            }
            
            // Check if package.json exists
            var packageJsonPath = Path.Combine(serverPath, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                UnityEngine.Debug.LogError($"[UnityVision] No package.json found at {serverPath}. Please select the correct MCP server directory.");
                return;
            }
            
            InstallServer(serverPath);
        }
        
        /// <summary>
        /// Menu item to rebuild MCP server
        /// </summary>
        [MenuItem("Window/UnityVision/Rebuild MCP Server")]
        public static void RebuildServerMenuItem()
        {
            var serverPath = GetServerPath();
            
            if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
            {
                UnityEngine.Debug.LogError("[UnityVision] MCP server path not configured. Use Window > UnityVision > Install MCP Server first.");
                return;
            }
            
            // Delete dist folder to force rebuild
            var distPath = Path.Combine(serverPath, "dist");
            if (Directory.Exists(distPath))
            {
                try
                {
                    Directory.Delete(distPath, true);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[UnityVision] Failed to delete dist folder: {ex.Message}");
                    return;
                }
            }
            
            RunNpmCommand("run build", serverPath);
        }
    }
}
