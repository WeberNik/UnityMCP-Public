// ============================================================================
// UnityVision Bridge - Configuration & Activity Tracking
// Shared configuration and request activity tracking for the bridge
// ============================================================================

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace UnityVision.Editor.Bridge
{
    /// <summary>
    /// Request activity entry for tracking recent requests
    /// </summary>
    public class RequestActivity
    {
        public DateTime Timestamp { get; set; }
        public string Method { get; set; }
        public int DurationMs { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Centralized configuration and activity tracking for the UnityVision bridge.
    /// Used by WebSocketClient and other components.
    /// </summary>
    [InitializeOnLoad]
    public static class BridgeConfig
    {
        private const int MAX_ACTIVITY_ENTRIES = 50;
        
        private static string _sessionToken;
        private static int _requestCount;
        private static List<RequestActivity> _recentActivity = new List<RequestActivity>();
        private static readonly object _activityLock = new object();

        /// <summary>
        /// Default WebSocket port for MCP server connection
        /// </summary>
        public static int DefaultPort { get; private set; } = 7890;
        
        /// <summary>
        /// Total number of requests processed
        /// </summary>
        public static int RequestCount => _requestCount;
        
        /// <summary>
        /// Session token for optional authentication
        /// </summary>
        public static string SessionToken => _sessionToken;
        
        /// <summary>
        /// Whether authentication is required
        /// </summary>
        public static bool RequireAuth { get; private set; } = false;
        
        /// <summary>
        /// Time of last request
        /// </summary>
        public static DateTime? LastRequestTime { get; private set; }
        
        /// <summary>
        /// Get recent request activity (thread-safe copy)
        /// </summary>
        public static List<RequestActivity> RecentActivity
        {
            get
            {
                lock (_activityLock)
                {
                    return new List<RequestActivity>(_recentActivity);
                }
            }
        }

        static BridgeConfig()
        {
            // Read port from environment variable if set
            var portEnv = Environment.GetEnvironmentVariable("UNITY_VISION_PORT");
            if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int port))
            {
                DefaultPort = port;
            }

            // Check if auth is required
            var authEnv = Environment.GetEnvironmentVariable("UNITY_VISION_REQUIRE_AUTH");
            RequireAuth = !string.IsNullOrEmpty(authEnv) && authEnv.ToLowerInvariant() == "true";

            // Generate session token
            _sessionToken = GenerateSessionToken();
            
            // Register project on startup
            EditorApplication.delayCall += () => Registry.ProjectRegistry.RegisterProject();
        }

        private static string GenerateSessionToken()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        /// <summary>
        /// Record a request in the activity log
        /// </summary>
        public static void RecordActivity(string method, int durationMs, bool success, string error = null)
        {
            lock (_activityLock)
            {
                _recentActivity.Insert(0, new RequestActivity
                {
                    Timestamp = DateTime.Now,
                    Method = method,
                    DurationMs = durationMs,
                    Success = success,
                    Error = error
                });
                
                // Trim to max entries
                while (_recentActivity.Count > MAX_ACTIVITY_ENTRIES)
                {
                    _recentActivity.RemoveAt(_recentActivity.Count - 1);
                }
            }
            
            _requestCount++;
            LastRequestTime = DateTime.Now;
        }
        
        /// <summary>
        /// Clear all recorded activity
        /// </summary>
        public static void ClearActivity()
        {
            lock (_activityLock)
            {
                _recentActivity.Clear();
            }
            _requestCount = 0;
            LastRequestTime = null;
        }
    }
}
