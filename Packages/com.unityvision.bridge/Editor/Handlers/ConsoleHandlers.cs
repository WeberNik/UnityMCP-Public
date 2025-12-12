// ============================================================================
// UnityVision Bridge - Console Handlers
// Handlers for Unity console log management
// Enhanced with reflection-based access for file/line info
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class ConsoleHandlers
    {
        #region Log Buffer

        private static readonly List<LogEntry> _logBuffer = new List<LogEntry>();
        private static readonly object _logLock = new object();
        private const int MaxLogEntries = 5000;
        private static bool _isSubscribed = false;

        // Reflection members for accessing internal LogEntry data
        private static bool _reflectionInitialized = false;
        private static bool _reflectionAvailable = false;
        private static MethodInfo _startGettingEntriesMethod;
        private static MethodInfo _endGettingEntriesMethod;
        private static MethodInfo _getCountMethod;
        private static MethodInfo _getEntryMethod;
        private static FieldInfo _modeField;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static FieldInfo _instanceIdField;
        private static Type _logEntryType;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (!_isSubscribed)
            {
                Application.logMessageReceivedThreaded += OnLogMessageReceived;
                _isSubscribed = true;
            }
            
            InitializeReflection();
        }

        private static void InitializeReflection()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            try
            {
                Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
                if (logEntriesType == null)
                {
                    Debug.LogWarning("[ConsoleHandlers] Could not find UnityEditor.LogEntries - file/line info unavailable");
                    return;
                }

                BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", staticFlags);
                _endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", staticFlags);
                _getCountMethod = logEntriesType.GetMethod("GetCount", staticFlags);
                _getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", staticFlags);

                _logEntryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry");
                if (_logEntryType == null)
                {
                    Debug.LogWarning("[ConsoleHandlers] Could not find UnityEditor.LogEntry");
                    return;
                }

                _modeField = _logEntryType.GetField("mode", instanceFlags);
                _messageField = _logEntryType.GetField("message", instanceFlags);
                _fileField = _logEntryType.GetField("file", instanceFlags);
                _lineField = _logEntryType.GetField("line", instanceFlags);
                _instanceIdField = _logEntryType.GetField("instanceID", instanceFlags);

                _reflectionAvailable = _startGettingEntriesMethod != null &&
                                       _endGettingEntriesMethod != null &&
                                       _getCountMethod != null &&
                                       _getEntryMethod != null &&
                                       _modeField != null &&
                                       _messageField != null;

                if (_reflectionAvailable)
                {
                    Debug.Log("[ConsoleHandlers] Reflection-based console access initialized successfully");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConsoleHandlers] Reflection initialization failed: {e.Message}");
                _reflectionAvailable = false;
            }
        }

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            lock (_logLock)
            {
                // Parse file/line from stack trace if possible
                string file = null;
                int line = 0;
                
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        stackTrace, 
                        @"\(at (.+):(\d+)\)",
                        System.Text.RegularExpressions.RegexOptions.None,
                        TimeSpan.FromMilliseconds(100)
                    );
                    if (match.Success)
                    {
                        file = match.Groups[1].Value;
                        int.TryParse(match.Groups[2].Value, out line);
                    }
                }

                _logBuffer.Add(new LogEntry
                {
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    type = type.ToString(),
                    message = message,
                    stackTrace = stackTrace,
                    file = file,
                    line = line
                });

                // Trim buffer if too large
                while (_logBuffer.Count > MaxLogEntries)
                {
                    _logBuffer.RemoveAt(0);
                }
            }
        }

        #endregion

        #region Request/Response Types

        [Serializable]
        public class LogEntry
        {
            public long timestamp;
            public string type;
            public string message;
            public string stackTrace;
            public string file;      // Source file
            public int line;         // Line number
            public int instanceId;   // Object instance ID
        }

        [Serializable]
        public class GetConsoleLogsRequest
        {
            public string level = "all";
            public int maxEntries = 200;
            public bool includeStackTrace = true;
            public long sinceTimeMs = 0;
        }

        [Serializable]
        public class GetConsoleLogsResponse
        {
            public List<LogEntry> entries;
        }

        [Serializable]
        public class ClearConsoleLogsResponse
        {
            public bool success;
        }

        #endregion

        public static RpcResponse GetConsoleLogs(RpcRequest request)
        {
            var req = request.GetParams<GetConsoleLogsRequest>();

            List<LogEntry> entries;
            lock (_logLock)
            {
                entries = _logBuffer
                    .Where(e => e.timestamp >= req.sinceTimeMs)
                    .Where(e => MatchesLevel(e.type, req.level))
                    .TakeLast(req.maxEntries)
                    .Select(e => new LogEntry
                    {
                        timestamp = e.timestamp,
                        type = e.type,
                        message = e.message,
                        stackTrace = req.includeStackTrace ? e.stackTrace : null,
                        file = e.file,
                        line = e.line,
                        instanceId = e.instanceId
                    })
                    .ToList();
            }

            return RpcResponse.Success(new GetConsoleLogsResponse { entries = entries });
        }

        /// <summary>
        /// Get console entries directly from Unity's internal LogEntries API
        /// This provides more accurate file/line info than parsing stack traces
        /// </summary>
        public static RpcResponse GetConsoleLogsFromUnity(RpcRequest request)
        {
            var req = request.GetParams<GetConsoleLogsRequest>();

            if (!_reflectionAvailable)
            {
                // Fall back to buffer-based approach
                return GetConsoleLogs(request);
            }

            var entries = new List<LogEntry>();

            try
            {
                _startGettingEntriesMethod.Invoke(null, null);
                int count = (int)_getCountMethod.Invoke(null, null);
                
                int startIndex = Math.Max(0, count - req.maxEntries);
                var logEntry = Activator.CreateInstance(_logEntryType);

                for (int i = startIndex; i < count; i++)
                {
                    _getEntryMethod.Invoke(null, new object[] { i, logEntry });

                    int mode = (int)_modeField.GetValue(logEntry);
                    string message = (string)_messageField.GetValue(logEntry);
                    string file = _fileField != null ? (string)_fileField.GetValue(logEntry) : null;
                    int line = _lineField != null ? (int)_lineField.GetValue(logEntry) : 0;
                    int instanceId = _instanceIdField != null ? (int)_instanceIdField.GetValue(logEntry) : 0;

                    // Convert mode to log type
                    string logType = ModeToLogType(mode);
                    
                    if (!MatchesLevel(logType, req.level))
                        continue;

                    entries.Add(new LogEntry
                    {
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), // Unity doesn't provide timestamp
                        type = logType,
                        message = message,
                        file = file,
                        line = line,
                        instanceId = instanceId
                    });
                }

                _endGettingEntriesMethod.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ConsoleHandlers] Reflection-based log access failed: {e.Message}");
                return GetConsoleLogs(request); // Fall back
            }

            return RpcResponse.Success(new GetConsoleLogsResponse { entries = entries });
        }

        /// <summary>
        /// Convert Unity's internal log mode flags to LogType string
        /// </summary>
        private static string ModeToLogType(int mode)
        {
            // Mode flags from Unity internals
            const int kError = 1;
            const int kAssert = 2;
            const int kLog = 4;
            const int kFatal = 16;
            const int kException = 256;
            const int kWarning = 512;

            if ((mode & kException) != 0) return "Exception";
            if ((mode & kError) != 0) return "Error";
            if ((mode & kAssert) != 0) return "Assert";
            if ((mode & kFatal) != 0) return "Error";
            if ((mode & kWarning) != 0) return "Warning";
            if ((mode & kLog) != 0) return "Log";
            
            return "Log";
        }

        public static RpcResponse ClearConsoleLogs(RpcRequest request)
        {
            lock (_logLock)
            {
                _logBuffer.Clear();
            }

            // Also clear the Unity console
            var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntries != null)
            {
                var clearMethod = logEntries.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                clearMethod?.Invoke(null, null);
            }

            return RpcResponse.Success(new ClearConsoleLogsResponse { success = true });
        }

        private static bool MatchesLevel(string logType, string level)
        {
            if (level == "all") return true;

            return level.ToLowerInvariant() switch
            {
                "error" => logType == "Error" || logType == "Exception" || logType == "Assert",
                "warning" => logType == "Warning",
                "info" => logType == "Log",
                _ => true
            };
        }

        /// <summary>
        /// Get recent errors for the Smart Context tool
        /// </summary>
        public static List<EditorHandlers.ConsoleErrorInfo> GetRecentErrors(int maxCount)
        {
            var errors = new List<EditorHandlers.ConsoleErrorInfo>();
            
            lock (_logLock)
            {
                var errorEntries = _logBuffer
                    .Where(e => e.type == "Error" || e.type == "Exception" || e.type == "Assert")
                    .TakeLast(maxCount)
                    .ToList();

                foreach (var entry in errorEntries)
                {
                    errors.Add(new EditorHandlers.ConsoleErrorInfo
                    {
                        timestamp = entry.timestamp,
                        type = entry.type,
                        message = entry.message.Length > 500 ? entry.message.Substring(0, 500) + "..." : entry.message
                    });
                }
            }

            return errors;
        }
    }
}
