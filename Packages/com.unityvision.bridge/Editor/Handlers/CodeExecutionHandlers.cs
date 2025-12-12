// ============================================================================
// UnityVision Bridge - Code Execution Handlers
// Runtime C# expression evaluation using reflection (no Mono.CSharp dependency)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    public static class CodeExecutionHandlers
    {
        #region Request/Response Types

        [Serializable]
        public class ExecuteCodeRequest
        {
            public string code;
            public bool captureOutput = true;
            public int timeoutMs = 5000;
        }

        [Serializable]
        public class ExecuteCodeResponse
        {
            public bool success;
            public object result;
            public string resultType;
            public string output;
            public string error;
            public float executionTimeMs;
        }

        [Serializable]
        public class EvaluateExpressionRequest
        {
            public string expression;
        }

        [Serializable]
        public class EvaluateExpressionResponse
        {
            public bool success;
            public object result;
            public string resultType;
            public string error;
        }

        #endregion

        private static StringBuilder _outputCapture;

        public static RpcResponse ExecuteCode(RpcRequest request)
        {
            var req = request.GetParams<ExecuteCodeRequest>();

            if (string.IsNullOrEmpty(req.code))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "code is required");
            }

            var startTime = DateTime.Now;
            _outputCapture = new StringBuilder();

            try
            {
                // Capture Debug.Log output
                Application.logMessageReceived += CaptureLog;

                var code = req.code.Trim();
                object result = null;

                // Try to evaluate as expression
                result = EvaluateExpressionInternal(code);

                Application.logMessageReceived -= CaptureLog;

                var executionTime = (float)(DateTime.Now - startTime).TotalMilliseconds;

                return RpcResponse.Success(new ExecuteCodeResponse
                {
                    success = true,
                    result = SerializeResult(result),
                    resultType = result?.GetType().FullName ?? "void",
                    output = _outputCapture.ToString(),
                    executionTimeMs = executionTime
                });
            }
            catch (Exception ex)
            {
                Application.logMessageReceived -= CaptureLog;

                return RpcResponse.Success(new ExecuteCodeResponse
                {
                    success = false,
                    error = ex.Message,
                    output = _outputCapture.ToString(),
                    executionTimeMs = (float)(DateTime.Now - startTime).TotalMilliseconds
                });
            }
        }

        public static RpcResponse EvaluateExpression(RpcRequest request)
        {
            var req = request.GetParams<EvaluateExpressionRequest>();

            if (string.IsNullOrEmpty(req.expression))
            {
                return RpcResponse.Failure("INVALID_PARAMS", "expression is required");
            }

            try
            {
                var result = EvaluateExpressionInternal(req.expression);

                return RpcResponse.Success(new EvaluateExpressionResponse
                {
                    success = true,
                    result = SerializeResult(result),
                    resultType = result?.GetType().FullName ?? "null"
                });
            }
            catch (Exception ex)
            {
                return RpcResponse.Success(new EvaluateExpressionResponse
                {
                    success = false,
                    error = ex.Message
                });
            }
        }

        #region Expression Evaluator

        /// <summary>
        /// Evaluates common Unity expressions using reflection.
        /// Supports: static properties, method calls, chained access, GameObject.Find, etc.
        /// </summary>
        private static object EvaluateExpressionInternal(string expression)
        {
            expression = expression.Trim().TrimEnd(';');

            if (string.IsNullOrEmpty(expression))
                return null;

            // Handle common patterns
            
            // Pattern: GameObject.Find("name")
            var goFindMatch = Regex.Match(expression, @"^GameObject\.Find\s*\(\s*""([^""]+)""\s*\)(.*)$");
            if (goFindMatch.Success)
            {
                var go = GameObject.Find(goFindMatch.Groups[1].Value);
                if (go == null) return null;
                var remainder = goFindMatch.Groups[2].Value;
                return string.IsNullOrEmpty(remainder) ? go : EvaluateChain(go, remainder);
            }

            // Pattern: FindObjectOfType<T>()
            var findTypeMatch = Regex.Match(expression, @"^FindObjectOfType<(\w+)>\s*\(\s*\)(.*)$");
            if (findTypeMatch.Success)
            {
                var typeName = findTypeMatch.Groups[1].Value;
                var type = FindType(typeName);
                if (type != null)
                {
                    var method = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
                    var genericMethod = method.MakeGenericMethod(type);
                    var result = genericMethod.Invoke(null, null);
                    var remainder = findTypeMatch.Groups[2].Value;
                    return string.IsNullOrEmpty(remainder) ? result : EvaluateChain(result, remainder);
                }
            }

            // Pattern: FindObjectsOfType<T>()
            var findTypesMatch = Regex.Match(expression, @"^FindObjectsOfType<(\w+)>\s*\(\s*\)(.*)$");
            if (findTypesMatch.Success)
            {
                var typeName = findTypesMatch.Groups[1].Value;
                var type = FindType(typeName);
                if (type != null)
                {
                    var method = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[0]);
                    var genericMethod = method.MakeGenericMethod(type);
                    var result = genericMethod.Invoke(null, null);
                    var remainder = findTypesMatch.Groups[2].Value;
                    return string.IsNullOrEmpty(remainder) ? result : EvaluateChain(result, remainder);
                }
            }

            // Pattern: Selection.activeGameObject
            if (expression.StartsWith("Selection."))
            {
                return EvaluateStaticChain(typeof(Selection), expression.Substring("Selection.".Length));
            }

            // Pattern: Camera.main
            if (expression.StartsWith("Camera."))
            {
                return EvaluateStaticChain(typeof(Camera), expression.Substring("Camera.".Length));
            }

            // Pattern: Application.*
            if (expression.StartsWith("Application."))
            {
                return EvaluateStaticChain(typeof(Application), expression.Substring("Application.".Length));
            }

            // Pattern: EditorApplication.*
            if (expression.StartsWith("EditorApplication."))
            {
                return EvaluateStaticChain(typeof(EditorApplication), expression.Substring("EditorApplication.".Length));
            }

            // Pattern: Time.*
            if (expression.StartsWith("Time."))
            {
                return EvaluateStaticChain(typeof(Time), expression.Substring("Time.".Length));
            }

            // Pattern: SceneManager.*
            if (expression.StartsWith("SceneManager."))
            {
                return EvaluateStaticChain(typeof(UnityEngine.SceneManagement.SceneManager), 
                    expression.Substring("SceneManager.".Length));
            }

            // Pattern: Numeric literals
            if (int.TryParse(expression, out int intVal)) return intVal;
            if (float.TryParse(expression, out float floatVal)) return floatVal;
            if (bool.TryParse(expression, out bool boolVal)) return boolVal;

            // Pattern: String literals
            if (expression.StartsWith("\"") && expression.EndsWith("\""))
                return expression.Substring(1, expression.Length - 2);

            // Pattern: null
            if (expression == "null") return null;

            // Try to find a type and evaluate static members
            var dotIndex = expression.IndexOf('.');
            if (dotIndex > 0)
            {
                var typePart = expression.Substring(0, dotIndex);
                var memberPart = expression.Substring(dotIndex + 1);
                var type = FindType(typePart);
                if (type != null)
                {
                    return EvaluateStaticChain(type, memberPart);
                }
            }

            throw new Exception($"Cannot evaluate expression: {expression}. " +
                "Supported patterns: GameObject.Find(\"name\"), Camera.main, Selection.activeGameObject, " +
                "FindObjectOfType<T>(), FindObjectsOfType<T>(), Application.*, Time.*, etc.");
        }

        private static object EvaluateStaticChain(Type type, string chain)
        {
            var parts = SplitChain(chain);
            object current = null;
            Type currentType = type;
            bool isStatic = true;

            foreach (var part in parts)
            {
                if (part.Contains("("))
                {
                    // Method call
                    var methodMatch = Regex.Match(part, @"^(\w+)\s*\((.*)\)$");
                    if (methodMatch.Success)
                    {
                        var methodName = methodMatch.Groups[1].Value;
                        var argsStr = methodMatch.Groups[2].Value;
                        var args = ParseArguments(argsStr);

                        var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                        var method = currentType.GetMethod(methodName, flags);
                        if (method != null)
                        {
                            current = method.Invoke(isStatic ? null : current, args);
                            currentType = current?.GetType();
                            isStatic = false;
                        }
                        else
                        {
                            throw new Exception($"Method '{methodName}' not found on type '{currentType.Name}'");
                        }
                    }
                }
                else if (part.Contains("["))
                {
                    // Indexer access
                    var indexMatch = Regex.Match(part, @"^(\w*)\[(\d+)\]$");
                    if (indexMatch.Success)
                    {
                        var propName = indexMatch.Groups[1].Value;
                        var index = int.Parse(indexMatch.Groups[2].Value);

                        if (!string.IsNullOrEmpty(propName))
                        {
                            var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                            var prop = currentType.GetProperty(propName, flags);
                            if (prop != null)
                            {
                                current = prop.GetValue(isStatic ? null : current);
                                currentType = current?.GetType();
                                isStatic = false;
                            }
                        }

                        // Apply indexer
                        if (current is Array arr)
                        {
                            current = arr.GetValue(index);
                            currentType = current?.GetType();
                        }
                        else if (current is System.Collections.IList list)
                        {
                            current = list[index];
                            currentType = current?.GetType();
                        }
                    }
                }
                else
                {
                    // Property or field access
                    var flags = BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance);
                    
                    var prop = currentType.GetProperty(part, flags);
                    if (prop != null)
                    {
                        current = prop.GetValue(isStatic ? null : current);
                        currentType = current?.GetType();
                        isStatic = false;
                        continue;
                    }

                    var field = currentType.GetField(part, flags);
                    if (field != null)
                    {
                        current = field.GetValue(isStatic ? null : current);
                        currentType = current?.GetType();
                        isStatic = false;
                        continue;
                    }

                    throw new Exception($"Member '{part}' not found on type '{currentType.Name}'");
                }
            }

            return current;
        }

        private static object EvaluateChain(object target, string chain)
        {
            if (target == null || string.IsNullOrEmpty(chain))
                return target;

            chain = chain.TrimStart('.');
            var parts = SplitChain(chain);
            object current = target;
            Type currentType = target.GetType();

            foreach (var part in parts)
            {
                if (current == null) return null;

                if (part.Contains("("))
                {
                    // Method call
                    var methodMatch = Regex.Match(part, @"^(\w+)\s*\((.*)\)$");
                    if (methodMatch.Success)
                    {
                        var methodName = methodMatch.Groups[1].Value;
                        var argsStr = methodMatch.Groups[2].Value;
                        var args = ParseArguments(argsStr);

                        var method = currentType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                        if (method != null)
                        {
                            current = method.Invoke(current, args);
                            currentType = current?.GetType();
                        }
                        else
                        {
                            throw new Exception($"Method '{methodName}' not found on type '{currentType.Name}'");
                        }
                    }
                }
                else if (part.Contains("["))
                {
                    // Indexer
                    var indexMatch = Regex.Match(part, @"^(\w*)\[(\d+)\]$");
                    if (indexMatch.Success)
                    {
                        var propName = indexMatch.Groups[1].Value;
                        var index = int.Parse(indexMatch.Groups[2].Value);

                        if (!string.IsNullOrEmpty(propName))
                        {
                            var prop = currentType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                            if (prop != null)
                            {
                                current = prop.GetValue(current);
                                currentType = current?.GetType();
                            }
                        }

                        if (current is Array arr)
                        {
                            current = arr.GetValue(index);
                            currentType = current?.GetType();
                        }
                        else if (current is System.Collections.IList list)
                        {
                            current = list[index];
                            currentType = current?.GetType();
                        }
                    }
                }
                else
                {
                    // Property or field
                    var prop = currentType.GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        current = prop.GetValue(current);
                        currentType = current?.GetType();
                        continue;
                    }

                    var field = currentType.GetField(part, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null)
                    {
                        current = field.GetValue(current);
                        currentType = current?.GetType();
                        continue;
                    }

                    throw new Exception($"Member '{part}' not found on type '{currentType.Name}'");
                }
            }

            return current;
        }

        private static List<string> SplitChain(string chain)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            int parenDepth = 0;
            int bracketDepth = 0;

            foreach (char c in chain)
            {
                if (c == '(') parenDepth++;
                else if (c == ')') parenDepth--;
                else if (c == '[') bracketDepth++;
                else if (c == ']') bracketDepth--;

                if (c == '.' && parenDepth == 0 && bracketDepth == 0)
                {
                    if (current.Length > 0)
                    {
                        parts.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
                parts.Add(current.ToString());

            return parts;
        }

        private static object[] ParseArguments(string argsStr)
        {
            if (string.IsNullOrWhiteSpace(argsStr))
                return new object[0];

            var args = new List<object>();
            var parts = argsStr.Split(',');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                
                if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                    args.Add(trimmed.Substring(1, trimmed.Length - 2));
                else if (int.TryParse(trimmed, out int i))
                    args.Add(i);
                else if (float.TryParse(trimmed, out float f))
                    args.Add(f);
                else if (bool.TryParse(trimmed, out bool b))
                    args.Add(b);
                else if (trimmed == "null")
                    args.Add(null);
                else
                    args.Add(trimmed);
            }

            return args.ToArray();
        }

        private static Type FindType(string typeName)
        {
            // Common Unity types
            var commonTypes = new Dictionary<string, Type>
            {
                { "GameObject", typeof(GameObject) },
                { "Transform", typeof(Transform) },
                { "Camera", typeof(Camera) },
                { "Light", typeof(Light) },
                { "Rigidbody", typeof(Rigidbody) },
                { "Collider", typeof(Collider) },
                { "BoxCollider", typeof(BoxCollider) },
                { "SphereCollider", typeof(SphereCollider) },
                { "MeshRenderer", typeof(MeshRenderer) },
                { "MeshFilter", typeof(MeshFilter) },
                { "AudioSource", typeof(AudioSource) },
                { "Animator", typeof(Animator) },
                { "Canvas", typeof(Canvas) },
                { "Image", typeof(UnityEngine.UI.Image) },
                { "Text", typeof(UnityEngine.UI.Text) },
                { "Button", typeof(UnityEngine.UI.Button) },
                { "Selection", typeof(Selection) },
                { "EditorApplication", typeof(EditorApplication) },
            };

            if (commonTypes.TryGetValue(typeName, out var type))
                return type;

            // Search all assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var foundType = assembly.GetType(typeName) ?? 
                                   assembly.GetType("UnityEngine." + typeName) ??
                                   assembly.GetType("UnityEditor." + typeName);
                    if (foundType != null) return foundType;
                }
                catch { }
            }

            return null;
        }

        #endregion

        private static void CaptureLog(string message, string stackTrace, LogType type)
        {
            _outputCapture?.AppendLine($"[{type}] {message}");
        }

        private static object SerializeResult(object result)
        {
            if (result == null) return null;

            var type = result.GetType();

            // Primitives and strings
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                return result;
            }

            // Unity types
            if (result is Vector2 v2) return new { x = v2.x, y = v2.y };
            if (result is Vector3 v3) return new { x = v3.x, y = v3.y, z = v3.z };
            if (result is Vector4 v4) return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (result is Quaternion q) return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (result is Color c) return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (result is Rect r) return new { x = r.x, y = r.y, width = r.width, height = r.height };
            if (result is Bounds b) return new { center = SerializeResult(b.center), size = SerializeResult(b.size) };

            // GameObject
            if (result is GameObject go)
            {
                return new
                {
                    name = go.name,
                    path = GameObjectHandlers.GetGameObjectPath(go),
                    active = go.activeSelf,
                    tag = go.tag,
                    layer = go.layer,
                    components = go.GetComponents<Component>()
                        .Where(c => c != null)
                        .Select(c => c.GetType().Name)
                        .ToList()
                };
            }

            // Component
            if (result is Component comp)
            {
                return new
                {
                    type = comp.GetType().Name,
                    gameObject = comp.gameObject.name,
                    path = GameObjectHandlers.GetGameObjectPath(comp.gameObject)
                };
            }

            // Arrays and collections
            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var items = new List<object>();
                int count = 0;
                foreach (var item in enumerable)
                {
                    if (count++ > 100) // Limit to prevent huge responses
                    {
                        items.Add("... (truncated)");
                        break;
                    }
                    items.Add(SerializeResult(item));
                }
                return items;
            }

            // Default: try to get meaningful string representation
            return result.ToString();
        }
    }
}
