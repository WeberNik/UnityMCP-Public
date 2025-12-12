// ============================================================================
// UnityVision Bridge - Script Handler
// CRUD operations for C# scripts
// Enhanced with atomic writes, path protection, and text edits
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Handlers
{
    /// <summary>
    /// Handles script management operations (create, read, update, delete, validate).
    /// </summary>
    public static class ScriptHandler
    {
        // ====================================================================
        // RPC Entry Point (for RpcHandler registration)
        // ====================================================================

        public static RpcResponse HandleRpc(RpcRequest request)
        {
            try
            {
                var args = request.Params?.ToObject<System.Collections.Generic.Dictionary<string, object>>();
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
                return RpcResponse.Failure("SCRIPT_ERROR", ex.Message);
            }
        }

        // ====================================================================
        // Main Entry Point
        // ====================================================================

        public static object Handle(string action, object parameters)
        {
            var args = parameters as System.Collections.Generic.Dictionary<string, object>;
            if (args == null)
            {
                return new { error = "Invalid parameters" };
            }

            string path = args.ContainsKey("path") ? args["path"]?.ToString() : null;
            if (string.IsNullOrEmpty(path))
            {
                return new { error = "Path is required" };
            }

            switch (action?.ToLower())
            {
                case "create":
                    return CreateScript(path, args);
                case "read":
                    return ReadScript(path);
                case "update":
                    return UpdateScript(path, args);
                case "delete":
                    return DeleteScript(path);
                case "validate":
                    return ValidateScript(path, args);
                case "get_sha":
                    return GetScriptSha(path);
                case "apply_text_edits":
                    return ApplyTextEdits(path, args);
                default:
                    return new { error = $"Unknown action: {action}. Valid actions: create, read, update, delete, validate, get_sha, apply_text_edits" };
            }
        }

        // ====================================================================
        // Create Script
        // ====================================================================

        private static object CreateScript(string relativePath, System.Collections.Generic.Dictionary<string, object> args)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                
                if (File.Exists(fullPath))
                {
                    return new { error = $"Script already exists: {relativePath}" };
                }

                // Get or generate contents
                string contents;
                if (args.ContainsKey("contents") && args["contents"] != null)
                {
                    contents = args["contents"].ToString();
                    
                    // Decode base64 if specified
                    if (args.ContainsKey("contentsEncoded") && args["contentsEncoded"] is bool encoded && encoded)
                    {
                        try
                        {
                            contents = Encoding.UTF8.GetString(Convert.FromBase64String(contents));
                        }
                        catch (Exception ex)
                        {
                            return new { error = $"Failed to decode base64 contents: {ex.Message}" };
                        }
                    }
                }
                else
                {
                    // Generate from template
                    string template = args.ContainsKey("template") ? args["template"]?.ToString() : "MonoBehaviour";
                    string className = args.ContainsKey("className") ? args["className"]?.ToString() : null;
                    string namespaceName = args.ContainsKey("namespace") ? args["namespace"]?.ToString() : null;
                    
                    if (string.IsNullOrEmpty(className))
                    {
                        className = Path.GetFileNameWithoutExtension(relativePath);
                    }
                    
                    contents = GenerateScriptFromTemplate(template, className, namespaceName);
                }

                // Validate path is under Assets/
                if (!ValidatePathUnderAssets(relativePath, out string validationError))
                {
                    return new { error = validationError };
                }

                // Ensure directory exists
                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Atomic write - write to temp, then move
                WriteFileAtomic(fullPath, contents);
                
                // Refresh AssetDatabase
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    path = relativePath,
                    fullPath = fullPath,
                    sha256 = ComputeSha256(contents),
                    size = contents.Length
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to create script: {ex.Message}" };
            }
        }

        // ====================================================================
        // Read Script
        // ====================================================================

        private static object ReadScript(string relativePath)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                
                if (!File.Exists(fullPath))
                {
                    return new { error = $"Script not found: {relativePath}" };
                }

                string contents = File.ReadAllText(fullPath, Encoding.UTF8);
                var fileInfo = new FileInfo(fullPath);

                return new
                {
                    success = true,
                    path = relativePath,
                    contents = contents,
                    sha256 = ComputeSha256(contents),
                    size = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTimeUtc.ToString("o")
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to read script: {ex.Message}" };
            }
        }

        // ====================================================================
        // Update Script
        // ====================================================================

        private static object UpdateScript(string relativePath, System.Collections.Generic.Dictionary<string, object> args)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                
                if (!File.Exists(fullPath))
                {
                    return new { error = $"Script not found: {relativePath}" };
                }

                // Check expected SHA if provided (conflict detection)
                if (args.ContainsKey("expectedSha") && args["expectedSha"] != null)
                {
                    string expectedSha = args["expectedSha"].ToString();
                    string currentContents = File.ReadAllText(fullPath, Encoding.UTF8);
                    string currentSha = ComputeSha256(currentContents);
                    
                    if (!string.Equals(expectedSha, currentSha, StringComparison.OrdinalIgnoreCase))
                    {
                        return new
                        {
                            error = "File has been modified since last read (SHA mismatch)",
                            expectedSha = expectedSha,
                            currentSha = currentSha
                        };
                    }
                }

                // Get new contents
                if (!args.ContainsKey("contents") || args["contents"] == null)
                {
                    return new { error = "Contents are required for update" };
                }

                string contents = args["contents"].ToString();
                
                // Decode base64 if specified
                if (args.ContainsKey("contentsEncoded") && args["contentsEncoded"] is bool encoded && encoded)
                {
                    try
                    {
                        contents = Encoding.UTF8.GetString(Convert.FromBase64String(contents));
                    }
                    catch (Exception ex)
                    {
                        return new { error = $"Failed to decode base64 contents: {ex.Message}" };
                    }
                }

                // Atomic write - write to temp, then move
                WriteFileAtomic(fullPath, contents);
                
                // Refresh AssetDatabase
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    path = relativePath,
                    sha256 = ComputeSha256(contents),
                    size = contents.Length
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to update script: {ex.Message}" };
            }
        }

        // ====================================================================
        // Apply Text Edits
        // ====================================================================

        private static object ApplyTextEdits(string relativePath, Dictionary<string, object> args)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                
                if (!File.Exists(fullPath))
                {
                    return new { error = $"Script not found: {relativePath}" };
                }

                // Check precondition SHA if provided
                string currentContents = File.ReadAllText(fullPath, Encoding.UTF8);
                if (args.ContainsKey("precondition_sha256") && args["precondition_sha256"] != null)
                {
                    string expectedSha = args["precondition_sha256"].ToString();
                    string currentSha = ComputeSha256(currentContents);
                    
                    if (!string.Equals(expectedSha, currentSha, StringComparison.OrdinalIgnoreCase))
                    {
                        return new
                        {
                            error = "Precondition failed: file has been modified (SHA mismatch)",
                            expectedSha = expectedSha,
                            currentSha = currentSha
                        };
                    }
                }

                // Get edits array
                if (!args.ContainsKey("edits") || args["edits"] == null)
                {
                    return new { error = "edits array is required" };
                }

                var editsObj = args["edits"];
                List<TextEdit> edits;
                
                // Parse edits from various formats
                if (editsObj is Newtonsoft.Json.Linq.JArray jArray)
                {
                    edits = jArray.ToObject<List<TextEdit>>();
                }
                else if (editsObj is List<object> listObj)
                {
                    edits = new List<TextEdit>();
                    foreach (var item in listObj)
                    {
                        if (item is Dictionary<string, object> dict)
                        {
                            edits.Add(new TextEdit
                            {
                                startLine = dict.ContainsKey("startLine") ? Convert.ToInt32(dict["startLine"]) : 0,
                                endLine = dict.ContainsKey("endLine") ? Convert.ToInt32(dict["endLine"]) : 0,
                                newText = dict.ContainsKey("newText") ? dict["newText"]?.ToString() : ""
                            });
                        }
                    }
                }
                else
                {
                    return new { error = "edits must be an array of {startLine, endLine, newText}" };
                }

                if (edits == null || edits.Count == 0)
                {
                    return new { error = "No edits provided" };
                }

                // Split content into lines
                var lines = currentContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                
                // Sort edits by startLine descending (apply from bottom to top to preserve line numbers)
                edits = edits.OrderByDescending(e => e.startLine).ToList();

                foreach (var edit in edits)
                {
                    // Validate line numbers (1-indexed)
                    if (edit.startLine < 1 || edit.endLine < edit.startLine)
                    {
                        return new { error = $"Invalid line range: {edit.startLine}-{edit.endLine}" };
                    }

                    int startIdx = edit.startLine - 1; // Convert to 0-indexed
                    int endIdx = Math.Min(edit.endLine - 1, lines.Count - 1);

                    // Remove old lines
                    int removeCount = endIdx - startIdx + 1;
                    if (startIdx < lines.Count)
                    {
                        lines.RemoveRange(startIdx, Math.Min(removeCount, lines.Count - startIdx));
                    }

                    // Insert new text
                    if (!string.IsNullOrEmpty(edit.newText))
                    {
                        var newLines = edit.newText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                        lines.InsertRange(startIdx, newLines);
                    }
                }

                // Reconstruct content
                string newContents = string.Join(Environment.NewLine, lines);

                // Atomic write
                WriteFileAtomic(fullPath, newContents);
                
                // Refresh AssetDatabase
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    path = relativePath,
                    sha256 = ComputeSha256(newContents),
                    size = newContents.Length,
                    editsApplied = edits.Count
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to apply text edits: {ex.Message}" };
            }
        }

        [Serializable]
        private class TextEdit
        {
            public int startLine;
            public int endLine;
            public string newText;
        }

        // ====================================================================
        // Delete Script
        // ====================================================================

        private static object DeleteScript(string relativePath)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                
                if (!File.Exists(fullPath))
                {
                    return new { error = $"Script not found: {relativePath}" };
                }

                // Delete the script file
                File.Delete(fullPath);
                
                // Delete the .meta file if it exists
                string metaPath = fullPath + ".meta";
                if (File.Exists(metaPath))
                {
                    File.Delete(metaPath);
                }
                
                // Refresh AssetDatabase
                AssetDatabase.Refresh();

                return new
                {
                    success = true,
                    path = relativePath,
                    deleted = true
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to delete script: {ex.Message}" };
            }
        }

        // ====================================================================
        // Validate Script
        // ====================================================================

        private static object ValidateScript(string relativePath, System.Collections.Generic.Dictionary<string, object> args)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                string contents;
                
                // Get contents from file or from args
                if (args.ContainsKey("contents") && args["contents"] != null)
                {
                    contents = args["contents"].ToString();
                    
                    if (args.ContainsKey("contentsEncoded") && args["contentsEncoded"] is bool encoded && encoded)
                    {
                        contents = Encoding.UTF8.GetString(Convert.FromBase64String(contents));
                    }
                }
                else if (File.Exists(fullPath))
                {
                    contents = File.ReadAllText(fullPath, Encoding.UTF8);
                }
                else
                {
                    return new { error = $"Script not found and no contents provided: {relativePath}" };
                }

                string level = args.ContainsKey("validationLevel") ? args["validationLevel"]?.ToString() : "basic";
                var errors = new System.Collections.Generic.List<object>();
                var warnings = new System.Collections.Generic.List<object>();

                // Basic validation: Check for obvious syntax issues
                ValidateBasicSyntax(contents, errors, warnings);

                // Standard validation: Check structure
                if (level == "standard" || level == "strict")
                {
                    ValidateStructure(contents, relativePath, errors, warnings);
                }

                return new
                {
                    success = errors.Count == 0,
                    path = relativePath,
                    validationLevel = level,
                    errors = errors,
                    warnings = warnings,
                    errorCount = errors.Count,
                    warningCount = warnings.Count
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to validate script: {ex.Message}" };
            }
        }

        // ====================================================================
        // Get SHA256
        // ====================================================================

        private static object GetScriptSha(string relativePath)
        {
            try
            {
                string fullPath = GetFullPath(relativePath);
                
                if (!File.Exists(fullPath))
                {
                    return new { error = $"Script not found: {relativePath}" };
                }

                string contents = File.ReadAllText(fullPath, Encoding.UTF8);
                var fileInfo = new FileInfo(fullPath);

                return new
                {
                    success = true,
                    path = relativePath,
                    sha256 = ComputeSha256(contents),
                    size = fileInfo.Length,
                    lastModified = fileInfo.LastWriteTimeUtc.ToString("o")
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to get SHA: {ex.Message}" };
            }
        }

        // ====================================================================
        // Helper Methods
        // ====================================================================

        private static string GetFullPath(string relativePath)
        {
            // Normalize path
            relativePath = relativePath.Replace('\\', '/');
            
            // Remove Assets/ prefix if present
            if (relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                relativePath = relativePath.Substring(7);
            }
            
            // Ensure .cs extension
            if (!relativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ".cs";
            }
            
            return Path.Combine(Application.dataPath, relativePath);
        }

        private static string ComputeSha256(string contents)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(contents);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static string GenerateScriptFromTemplate(string template, string className, string namespaceName)
        {
            var sb = new StringBuilder();
            
            // Add using statements
            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            
            if (template == "Editor" || template == "EditorWindow")
            {
                sb.AppendLine("using UnityEditor;");
            }
            
            sb.AppendLine();
            
            // Add namespace if specified
            bool hasNamespace = !string.IsNullOrEmpty(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }
            
            string indent = hasNamespace ? "    " : "";
            
            // Generate class based on template
            switch (template?.ToLower())
            {
                case "scriptableobject":
                    sb.AppendLine($"{indent}[CreateAssetMenu(fileName = \"{className}\", menuName = \"ScriptableObjects/{className}\")]");
                    sb.AppendLine($"{indent}public class {className} : ScriptableObject");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case "editor":
                    sb.AppendLine($"{indent}[CustomEditor(typeof(MonoBehaviour))] // TODO: Change target type");
                    sb.AppendLine($"{indent}public class {className} : UnityEditor.Editor");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    public override void OnInspectorGUI()");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        base.OnInspectorGUI();");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case "editorwindow":
                    sb.AppendLine($"{indent}public class {className} : EditorWindow");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    [MenuItem(\"Window/{className}\")]");
                    sb.AppendLine($"{indent}    public static void ShowWindow()");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}        GetWindow<{className}>(\"{className}\");");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine();
                    sb.AppendLine($"{indent}    private void OnGUI()");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case "empty":
                    sb.AppendLine($"{indent}public class {className}");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}}}");
                    break;
                    
                case "monobehaviour":
                default:
                    sb.AppendLine($"{indent}public class {className} : MonoBehaviour");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    private void Start()");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine();
                    sb.AppendLine($"{indent}    private void Update()");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine($"{indent}    }}");
                    sb.AppendLine($"{indent}}}");
                    break;
            }
            
            if (hasNamespace)
            {
                sb.AppendLine("}");
            }
            
            return sb.ToString();
        }

        private static void ValidateBasicSyntax(string contents, System.Collections.Generic.List<object> errors, System.Collections.Generic.List<object> warnings)
        {
            // Check for balanced braces
            int braceCount = 0;
            int lineNumber = 1;
            foreach (char c in contents)
            {
                if (c == '{') braceCount++;
                else if (c == '}') braceCount--;
                else if (c == '\n') lineNumber++;
                
                if (braceCount < 0)
                {
                    errors.Add(new { line = lineNumber, message = "Unexpected closing brace" });
                    break;
                }
            }
            
            if (braceCount > 0)
            {
                errors.Add(new { line = lineNumber, message = $"Missing {braceCount} closing brace(s)" });
            }
            else if (braceCount < 0)
            {
                errors.Add(new { line = lineNumber, message = $"Extra {-braceCount} closing brace(s)" });
            }

            // Check for balanced parentheses
            int parenCount = 0;
            lineNumber = 1;
            foreach (char c in contents)
            {
                if (c == '(') parenCount++;
                else if (c == ')') parenCount--;
                else if (c == '\n') lineNumber++;
            }
            
            if (parenCount != 0)
            {
                errors.Add(new { line = lineNumber, message = "Unbalanced parentheses" });
            }

            // Check for common issues
            if (contents.Contains(";;"))
            {
                warnings.Add(new { message = "Double semicolon detected" });
            }
        }

        private static void ValidateStructure(string contents, string relativePath, System.Collections.Generic.List<object> errors, System.Collections.Generic.List<object> warnings)
        {
            // Check for class declaration
            if (!Regex.IsMatch(contents, @"\b(class|struct|interface|enum)\s+\w+"))
            {
                errors.Add(new { message = "No class, struct, interface, or enum declaration found" });
            }

            // Check if class name matches filename
            string expectedClassName = Path.GetFileNameWithoutExtension(relativePath);
            if (!Regex.IsMatch(contents, $@"\b(class|struct)\s+{Regex.Escape(expectedClassName)}\b"))
            {
                warnings.Add(new { message = $"Class name should match filename: {expectedClassName}" });
            }

            // Check for using statements
            if (!contents.Contains("using "))
            {
                warnings.Add(new { message = "No using statements found" });
            }
        }

        // ====================================================================
        // Atomic File Write
        // ====================================================================

        /// <summary>
        /// Write file atomically by writing to temp file first, then moving.
        /// This prevents file corruption if the process is interrupted.
        /// </summary>
        private static void WriteFileAtomic(string fullPath, string contents)
        {
            string tempPath = fullPath + ".tmp";
            
            try
            {
                // Write to temp file (UTF8 without BOM for C# files)
                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(tempPath, contents, utf8NoBom);
                
                // Atomic move (replace if exists)
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                File.Move(tempPath, fullPath);
            }
            catch
            {
                // Fallback: try direct write if atomic fails
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch { /* Ignore cleanup errors */ }
                
                // Direct write as fallback
                var utf8NoBom = new UTF8Encoding(false);
                File.WriteAllText(fullPath, contents, utf8NoBom);
            }
        }

        // ====================================================================
        // Path Validation
        // ====================================================================

        /// <summary>
        /// Validate that a path is under the Assets/ folder and doesn't use path traversal.
        /// </summary>
        private static bool ValidatePathUnderAssets(string relativePath, out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(relativePath))
            {
                error = "Path cannot be empty";
                return false;
            }

            // Normalize the path
            string normalized = relativePath.Replace('\\', '/').Trim('/');

            // Check for path traversal attempts
            if (normalized.Contains(".."))
            {
                error = "Path traversal (..) is not allowed";
                return false;
            }

            // Must be under Assets/ or Packages/
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase))
            {
                error = "Path must be under Assets/ or Packages/";
                return false;
            }

            // Check for symlinks/reparse points (Windows)
            string fullPath = GetFullPath(relativePath);
            string directory = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(directory);
                    if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        error = "Symlinks/reparse points are not allowed in path";
                        return false;
                    }
                }
                catch
                {
                    // Ignore attribute check errors
                }
            }

            return true;
        }
    }
}
