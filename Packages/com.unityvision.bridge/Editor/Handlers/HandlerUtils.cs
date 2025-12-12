// ============================================================================
// UnityVision Bridge - Handler Utilities
// Shared utilities to reduce code duplication across handlers
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    /// <summary>
    /// Common utilities shared across all handlers to reduce code duplication.
    /// </summary>
    public static class HandlerUtils
    {
        /// <summary>
        /// Safely execute a handler function with standard error handling.
        /// Returns RpcResponse with success or failure.
        /// </summary>
        public static RpcResponse SafeExecute<TRequest, TResponse>(
            RpcRequest request,
            Func<TRequest, TResponse> handler,
            string errorCode = "HANDLER_ERROR")
            where TRequest : class, new()
        {
            try
            {
                var req = request.GetParams<TRequest>() ?? new TRequest();
                var result = handler(req);
                return RpcResponse.Success(result);
            }
            catch (ArgumentException ex)
            {
                return RpcResponse.Failure("INVALID_PARAMS", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return RpcResponse.Failure("INVALID_OPERATION", ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVision] Handler error: {ex.Message}\n{ex.StackTrace}");
                return RpcResponse.Failure(errorCode, ex.Message);
            }
        }

        /// <summary>
        /// Safely execute a handler function that returns an object directly.
        /// </summary>
        public static RpcResponse SafeExecute<TRequest>(
            RpcRequest request,
            Func<TRequest, object> handler,
            string errorCode = "HANDLER_ERROR")
            where TRequest : class, new()
        {
            try
            {
                var req = request.GetParams<TRequest>() ?? new TRequest();
                var result = handler(req);
                return RpcResponse.Success(result);
            }
            catch (ArgumentException ex)
            {
                return RpcResponse.Failure("INVALID_PARAMS", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return RpcResponse.Failure("INVALID_OPERATION", ex.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVision] Handler error: {ex.Message}\n{ex.StackTrace}");
                return RpcResponse.Failure(errorCode, ex.Message);
            }
        }

        /// <summary>
        /// Safely execute a simple handler with no request parameters.
        /// </summary>
        public static RpcResponse SafeExecute<TResponse>(
            Func<TResponse> handler,
            string errorCode = "HANDLER_ERROR")
        {
            try
            {
                var result = handler();
                return RpcResponse.Success(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVision] Handler error: {ex.Message}\n{ex.StackTrace}");
                return RpcResponse.Failure(errorCode, ex.Message);
            }
        }

        /// <summary>
        /// Validate that a required string parameter is not null or empty.
        /// Throws ArgumentException if invalid.
        /// </summary>
        public static void RequireString(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException($"{paramName} is required");
            }
        }

        /// <summary>
        /// Validate that a required object parameter is not null.
        /// Throws ArgumentException if invalid.
        /// </summary>
        public static void RequireNotNull(object value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentException($"{paramName} is required");
            }
        }

        /// <summary>
        /// Get a value from a dictionary with a default fallback.
        /// </summary>
        public static T GetValueOrDefault<T>(Dictionary<string, object> dict, string key, T defaultValue = default)
        {
            if (dict == null || !dict.TryGetValue(key, out var value) || value == null)
            {
                return defaultValue;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Parse an enum value from string, with default fallback.
        /// </summary>
        public static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue) where TEnum : struct
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            if (Enum.TryParse<TEnum>(value, true, out var result))
            {
                return result;
            }

            return defaultValue;
        }

        /// <summary>
        /// Create a standard success response with a message.
        /// </summary>
        public static object SuccessResult(string message = "Success")
        {
            return new { success = true, message };
        }

        /// <summary>
        /// Create a standard error result (for use within handlers).
        /// </summary>
        public static object ErrorResult(string error)
        {
            return new { success = false, error };
        }

        /// <summary>
        /// Clamp an integer value to a range.
        /// </summary>
        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        /// <summary>
        /// Safely get a component from a GameObject, returning null if not found.
        /// </summary>
        public static T GetComponentSafe<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            return go.GetComponent<T>();
        }

        /// <summary>
        /// Format a timestamp for consistent output.
        /// </summary>
        public static string FormatTimestamp(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        /// <summary>
        /// Format a Unix timestamp (milliseconds) to DateTime.
        /// </summary>
        public static DateTime FromUnixTimeMs(long unixTimeMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs).LocalDateTime;
        }

        /// <summary>
        /// Get current Unix timestamp in milliseconds.
        /// </summary>
        public static long GetUnixTimeMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
