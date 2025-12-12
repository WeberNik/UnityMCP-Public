// ============================================================================
// UnityVision Bridge - Project Registry
// Manages registration of Unity projects for multi-project MCP support
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using UnityVision.Editor.Bridge;

namespace UnityVision.Editor.Registry
{
    /// <summary>
    /// Registry entry for a Unity project
    /// </summary>
    [Serializable]
    public class ProjectEntry
    {
        public string projectPath;
        public string projectName;
        public string pipeName;  // Named pipe identifier (e.g., "unityvision-a1b2c3d4")
        public int port;         // Legacy HTTP port (kept for backwards compatibility)
        public int pid;
        public string unityVersion;
        public string lastSeen;
        public bool isActive;
        
        public ProjectEntry()
        {
            lastSeen = DateTime.UtcNow.ToString("o");
            isActive = true;
        }
    }

    /// <summary>
    /// Manages the global project registry file at ~/.unityvision/projects.json
    /// Allows MCP server to discover and connect to multiple Unity instances
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectRegistry
    {
        private const string REGISTRY_FOLDER = ".unityvision";
        private const string REGISTRY_FILE = "projects.json";
        private const float HEARTBEAT_INTERVAL = 5f; // seconds
        
        private static string _registryPath;
        private static double _lastHeartbeat;
        private static ProjectEntry _currentEntry;
        
        /// <summary>
        /// Path to the registry file
        /// </summary>
        public static string RegistryPath => _registryPath;
        
        /// <summary>
        /// Current project's registry entry
        /// </summary>
        public static ProjectEntry CurrentEntry => _currentEntry;

        static ProjectRegistry()
        {
            // Determine registry path based on platform
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string registryFolder = Path.Combine(homeDir, REGISTRY_FOLDER);
            _registryPath = Path.Combine(registryFolder, REGISTRY_FILE);
            
            // Ensure directory exists
            if (!Directory.Exists(registryFolder))
            {
                try
                {
                    Directory.CreateDirectory(registryFolder);
                    Debug.Log($"[UnityVision] Created registry folder: {registryFolder}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[UnityVision] Failed to create registry folder: {ex.Message}");
                    return;
                }
            }
            
            // Register this project
            RegisterProject();
            
            // Set up heartbeat
            EditorApplication.update += Heartbeat;
            
            // Unregister on quit
            EditorApplication.quitting += UnregisterProject;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
        }

        /// <summary>
        /// Register this Unity project in the global registry
        /// </summary>
        public static void RegisterProject()
        {
            try
            {
                var entries = LoadRegistry();
                
                // Find or create entry for this project
                string projectPath = GetProjectPath();
                int existingIndex = entries.FindIndex(e => e.projectPath == projectPath);
                
                _currentEntry = new ProjectEntry
                {
                    projectPath = projectPath,
                    projectName = GetProjectName(),
                    pipeName = NamedPipeBridge.PipeName,  // Named pipe identifier
                    port = BridgeConfig.DefaultPort,  // WebSocket port
                    pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                    unityVersion = Application.unityVersion,
                    lastSeen = DateTime.UtcNow.ToString("o"),
                    isActive = true
                };
                
                if (existingIndex >= 0)
                {
                    entries[existingIndex] = _currentEntry;
                }
                else
                {
                    entries.Add(_currentEntry);
                }
                
                SaveRegistry(entries);
                Debug.Log($"[UnityVision] Registered project: {_currentEntry.projectName} (pipe: {_currentEntry.pipeName})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVision] Failed to register project: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregister this Unity project from the global registry
        /// </summary>
        public static void UnregisterProject()
        {
            try
            {
                var entries = LoadRegistry();
                string projectPath = GetProjectPath();
                
                int index = entries.FindIndex(e => e.projectPath == projectPath);
                if (index >= 0)
                {
                    entries[index].isActive = false;
                    entries[index].lastSeen = DateTime.UtcNow.ToString("o");
                    SaveRegistry(entries);
                    Debug.Log($"[UnityVision] Unregistered project: {entries[index].projectName}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityVision] Failed to unregister project: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the heartbeat timestamp for this project
        /// </summary>
        public static void UpdateHeartbeat()
        {
            if (_currentEntry == null) return;
            
            try
            {
                var entries = LoadRegistry();
                string projectPath = GetProjectPath();
                
                int index = entries.FindIndex(e => e.projectPath == projectPath);
                if (index >= 0)
                {
                    entries[index].lastSeen = DateTime.UtcNow.ToString("o");
                    entries[index].isActive = true;
                    entries[index].port = Bridge.BridgeConfig.DefaultPort; // Update port in case it changed
                    _currentEntry = entries[index];
                    SaveRegistry(entries);
                }
                else
                {
                    // Re-register if entry was removed
                    RegisterProject();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityVision] Failed to update heartbeat: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all registered projects
        /// </summary>
        public static List<ProjectEntry> GetAllProjects()
        {
            return LoadRegistry();
        }

        /// <summary>
        /// Get all active (recently seen) projects
        /// </summary>
        public static List<ProjectEntry> GetActiveProjects(int staleThresholdSeconds = 30)
        {
            var entries = LoadRegistry();
            var now = DateTime.UtcNow;
            
            return entries.FindAll(e => 
            {
                if (!e.isActive) return false;
                
                if (DateTime.TryParse(e.lastSeen, out DateTime lastSeen))
                {
                    return (now - lastSeen).TotalSeconds < staleThresholdSeconds;
                }
                return false;
            });
        }

        /// <summary>
        /// Clean up stale entries from the registry
        /// </summary>
        public static void CleanupStaleEntries(int staleThresholdSeconds = 300)
        {
            try
            {
                var entries = LoadRegistry();
                var now = DateTime.UtcNow;
                string currentPath = GetProjectPath();
                
                entries.RemoveAll(e =>
                {
                    // Don't remove current project
                    if (e.projectPath == currentPath) return false;
                    
                    // Remove inactive entries older than threshold
                    if (!e.isActive && DateTime.TryParse(e.lastSeen, out DateTime lastSeen))
                    {
                        return (now - lastSeen).TotalSeconds > staleThresholdSeconds;
                    }
                    return false;
                });
                
                SaveRegistry(entries);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityVision] Failed to cleanup stale entries: {ex.Message}");
            }
        }

        private static void Heartbeat()
        {
            if (EditorApplication.timeSinceStartup - _lastHeartbeat < HEARTBEAT_INTERVAL)
                return;
            
            _lastHeartbeat = EditorApplication.timeSinceStartup;
            UpdateHeartbeat();
        }

        private static void OnBeforeAssemblyReload()
        {
            // Mark as temporarily inactive during reload
            if (_currentEntry != null)
            {
                try
                {
                    var entries = LoadRegistry();
                    string projectPath = GetProjectPath();
                    int index = entries.FindIndex(e => e.projectPath == projectPath);
                    if (index >= 0)
                    {
                        entries[index].lastSeen = DateTime.UtcNow.ToString("o");
                        // Keep isActive true during reload
                        SaveRegistry(entries);
                    }
                }
                catch { }
            }
        }

        private static void OnAfterAssemblyReload()
        {
            // Re-register after reload
            RegisterProject();
        }

        private static List<ProjectEntry> LoadRegistry()
        {
            if (!File.Exists(_registryPath))
            {
                return new List<ProjectEntry>();
            }
            
            try
            {
                string json = File.ReadAllText(_registryPath);
                var entries = JsonConvert.DeserializeObject<List<ProjectEntry>>(json);
                return entries ?? new List<ProjectEntry>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityVision] Failed to load registry: {ex.Message}");
                return new List<ProjectEntry>();
            }
        }

        private static void SaveRegistry(List<ProjectEntry> entries)
        {
            try
            {
                string json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                File.WriteAllText(_registryPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityVision] Failed to save registry: {ex.Message}");
            }
        }

        private static string GetProjectPath()
        {
            // Use Application.dataPath and go up one level to get project root
            string dataPath = Application.dataPath;
            return Directory.GetParent(dataPath)?.FullName ?? dataPath;
        }

        private static string GetProjectName()
        {
            string projectPath = GetProjectPath();
            return Path.GetFileName(projectPath) ?? "Unknown Project";
        }
    }
}
