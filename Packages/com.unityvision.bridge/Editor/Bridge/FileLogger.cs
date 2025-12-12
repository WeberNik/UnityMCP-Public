// ============================================================================
// UnityVision MCP Bridge - File Logger
// Writes detailed debug logs to a file for troubleshooting
// ============================================================================

using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace UnityVision.Editor.Bridge
{
    /// <summary>
    /// File-based logger for debugging MCP bridge issues.
    /// Writes to Logs/UnityVision_Debug.log in the project folder.
    /// </summary>
    public static class FileLogger
    {
        private static readonly object _lock = new object();
        private static string _logPath;
        private static bool _initialized;
        private const long MAX_LOG_SIZE = 5 * 1024 * 1024; // 5MB
        
        public static bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Get the log file path
        /// </summary>
        public static string LogPath
        {
            get
            {
                EnsureInitialized();
                return _logPath;
            }
        }
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            lock (_lock)
            {
                if (_initialized) return;
                
                try
                {
                    string projectPath = Application.dataPath;
                    if (projectPath.EndsWith("/Assets"))
                        projectPath = projectPath.Substring(0, projectPath.Length - 7);
                    
                    string logsDir = Path.Combine(projectPath, "Logs");
                    if (!Directory.Exists(logsDir))
                        Directory.CreateDirectory(logsDir);
                    
                    _logPath = Path.Combine(logsDir, "UnityVision_Debug.log");
                    _initialized = true;
                    
                    // Write header
                    Log("INFO", "INIT", $"FileLogger initialized. Unity {Application.unityVersion}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnityVision] Failed to initialize file logger: {ex.Message}");
                    _initialized = true; // Prevent repeated attempts
                }
            }
        }
        
        /// <summary>
        /// Rotate log file if too large
        /// </summary>
        private static void RotateIfNeeded()
        {
            try
            {
                if (File.Exists(_logPath))
                {
                    var info = new FileInfo(_logPath);
                    if (info.Length > MAX_LOG_SIZE)
                    {
                        string backupPath = _logPath + ".old";
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                        File.Move(_logPath, backupPath);
                    }
                }
            }
            catch
            {
                // Ignore rotation errors
            }
        }
        
        /// <summary>
        /// Write a log entry
        /// </summary>
        public static void Log(string level, string component, string message, object data = null)
        {
            if (!Enabled) return;
            
            EnsureInitialized();
            if (string.IsNullOrEmpty(_logPath)) return;
            
            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();
                    
                    var sb = new StringBuilder();
                    sb.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{component}] {message}");
                    
                    if (data != null)
                    {
                        try
                        {
                            string dataStr = JsonUtility.ToJson(data);
                            if (string.IsNullOrEmpty(dataStr) || dataStr == "{}")
                            {
                                dataStr = data.ToString();
                            }
                            if (dataStr.Length > 2000)
                            {
                                sb.Append($" | DATA: {dataStr.Substring(0, 2000)}... (truncated)");
                            }
                            else
                            {
                                sb.Append($" | DATA: {dataStr}");
                            }
                        }
                        catch
                        {
                            sb.Append($" | DATA: {data}");
                        }
                    }
                    
                    sb.AppendLine();
                    File.AppendAllText(_logPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Silently fail
            }
        }
        
        /// <summary>
        /// Log request start
        /// </summary>
        public static void LogRequestStart(string requestId, string method)
        {
            Log("INFO", "REQUEST", $"START {requestId} - {method}");
        }
        
        /// <summary>
        /// Log request phase
        /// </summary>
        public static void LogRequestPhase(string requestId, string phase, string details = null)
        {
            Log("DEBUG", "REQUEST", $"PHASE {requestId} - {phase}" + (details != null ? $" - {details}" : ""));
        }
        
        /// <summary>
        /// Log request end
        /// </summary>
        public static void LogRequestEnd(string requestId, string method, int durationMs, bool success, string error = null)
        {
            string level = success ? "INFO" : "ERROR";
            string status = success ? "SUCCESS" : $"FAILED: {error}";
            Log(level, "REQUEST", $"END {requestId} - {method} - {durationMs}ms - {status}");
        }
        
        /// <summary>
        /// Log an error
        /// </summary>
        public static void LogError(string component, string message, Exception ex = null)
        {
            Log("ERROR", component, message + (ex != null ? $" | Exception: {ex.Message}" : ""));
        }
        
        /// <summary>
        /// Clear the log file
        /// </summary>
        public static void Clear()
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(_logPath)) return;
            
            try
            {
                lock (_lock)
                {
                    File.WriteAllText(_logPath, $"=== Log cleared at {DateTime.Now:O} ===\n", Encoding.UTF8);
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}
