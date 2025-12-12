// ============================================================================
// UnityVision MCP Server - Consolidated Tools
// Groups related tools under single tool names with action parameter
// Reduces tool count from 72 to ~24 while maintaining full functionality
// ============================================================================

import { z } from 'zod';

// Import all original handlers
import {
  getEditorState,
  setPlayMode,
  getActiveContext,
  recompileScripts,
  refreshAssets,
} from './editorTools.js';

import {
  getConsoleLogs,
  clearConsoleLogs,
} from './consoleTools.js';

import {
  listScenes,
  getSceneHierarchy,
  createScene,
  loadScene,
  saveScene,
  deleteScene,
} from './sceneTools.js';

import {
  createGameObject,
  modifyGameObject,
  deleteGameObject,
} from './gameObjectTools.js';

import {
  addComponent,
  setComponentProperties,
  searchComponentTypes,
} from './componentTools.js';

import {
  dumpUILayout,
} from './uiTools.js';

import {
  captureGameViewScreenshot,
  captureSceneViewScreenshot,
} from './screenshotTools.js';

import {
  setXRRigPose,
  teleportXRRigToAnchor,
} from './xrTools.js';

import {
  runTests,
} from './testTools.js';

import {
  buildPlayer,
} from './buildTools.js';

import {
  executeCode,
  evaluateExpression,
} from './codeExecutionTools.js';

import {
  executeMenuItem,
  listMenuItems,
} from './menuItemTools.js';

import {
  searchAssets,
  createFolder,
  moveAsset,
  deleteAsset,
  createPrefab,
  instantiatePrefab,
  getAssetInfo,
} from './assetTools.js';

import {
  batchExecute,
} from './batchTools.js';

import {
  handleListUnityProjects,
  handleSwitchProject,
  handleGetActiveProject,
} from './projectTools.js';

import {
  getEditorSelection,
  setEditorSelection,
} from './selectionTools.js';

import {
  getComponentProperties,
  setComponentProperty,
  compareComponents,
} from './inspectorTools.js';

import {
  getMaterialProperties,
  setMaterialProperty,
  listMaterials,
  listShaders,
} from './materialTools.js';

import {
  findObjectsWithComponent,
  findMissingReferences,
  analyzeLayers,
  findObjectsInRadius,
} from './queryTools.js';

import {
  getPrefabOverrides,
  applyPrefabOverrides,
  revertPrefabOverrides,
  findPrefabInstances,
} from './prefabTools.js';

import {
  findAssetReferences,
  getAssetDependencies,
  findUnusedAssets,
} from './dependencyTools.js';

import {
  getRenderingStats,
  getMemorySnapshot,
  getPerformanceRecommendations,
} from './profilerTools.js';

import {
  getAnimatorState,
  setAnimatorParameter,
  getAnimationClips,
  playAnimation,
  sampleAnimation,
} from './animationTools.js';

import {
  listAudioSources,
  getAudioClipInfo,
  listAudioClips,
  previewAudio,
  setAudioSource,
} from './audioTools.js';

import {
  shaderGraphToolHandlers,
} from './shaderGraphTools.js';

import {
  scriptToolDefinition,
  executeScriptTool,
} from './scriptTools.js';

import {
  packageToolDefinition,
  executePackageTool,
} from './packageTools.js';

// ============================================================================
// Consolidated Tool Definitions
// ============================================================================

export const consolidatedToolDefinitions = [
  // 1. unity_editor - Editor state and play mode control
  {
    name: 'unity_editor',
    description: 'Control Unity Editor state. Actions: get_state, set_play_mode, get_context, recompile, refresh',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get_state', 'set_play_mode', 'get_context', 'recompile', 'refresh'],
          description: 'Action to perform',
        },
        mode: {
          type: 'string',
          enum: ['play', 'pause', 'stop'],
          description: 'For set_play_mode: play mode to set',
        },
        includeSceneInfo: {
          type: 'boolean',
          description: 'For get_context: include detailed scene info',
        },
      },
      required: ['action'],
    },
  },

  // 2. unity_console - Console log management
  {
    name: 'unity_console',
    description: 'Manage Unity console logs. Actions: get_logs (retrieve logs with optional filters), clear (clear all logs)',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get_logs', 'clear'],
          description: 'Action to perform',
        },
        logType: {
          type: 'string',
          enum: ['all', 'error', 'warning', 'log'],
          description: 'For get_logs: filter by log type',
        },
        count: {
          type: 'number',
          description: 'For get_logs: max number of logs to return',
        },
        searchText: {
          type: 'string',
          description: 'For get_logs: filter logs containing this text',
        },
      },
      required: ['action'],
    },
  },

  // 3. unity_scene - Scene management
  {
    name: 'unity_scene',
    description: 'Manage Unity scenes. Actions: list, hierarchy, create, load, save, delete',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['list', 'hierarchy', 'create', 'load', 'save', 'delete'],
          description: 'Action to perform',
        },
        sceneName: {
          type: 'string',
          description: 'For hierarchy: specific scene name (optional)',
        },
        path: {
          type: 'string',
          description: 'For create/load/save/delete: scene path (e.g., "Assets/Scenes/MainMenu.unity")',
        },
        template: {
          type: 'string',
          description: 'For create: scene template (default, empty)',
        },
        additive: {
          type: 'boolean',
          description: 'For load: load additively (keep current scene)',
        },
        saveAs: {
          type: 'string',
          description: 'For save: save to new path',
        },
        maxDepth: {
          type: 'number',
          description: 'For hierarchy: max depth to traverse',
        },
        includeInactive: {
          type: 'boolean',
          description: 'For hierarchy: include inactive objects',
        },
      },
      required: ['action'],
    },
  },

  // 4. unity_gameobject - GameObject CRUD operations
  {
    name: 'unity_gameobject',
    description: 'Create, modify, or delete GameObjects. Actions: create, modify, delete',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['create', 'modify', 'delete'],
          description: 'Action to perform',
        },
        name: {
          type: 'string',
          description: 'GameObject name (for create/modify)',
        },
        targetId: {
          type: 'string',
          description: 'Target GameObject ID (for modify/delete)',
        },
        parentId: {
          type: 'string',
          description: 'Parent GameObject ID',
        },
        position: {
          type: 'object',
          properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
          description: 'World position',
        },
        rotation: {
          type: 'object',
          properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
          description: 'Euler rotation',
        },
        scale: {
          type: 'object',
          properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
          description: 'Local scale',
        },
        active: {
          type: 'boolean',
          description: 'Active state',
        },
        primitiveType: {
          type: 'string',
          enum: ['Cube', 'Sphere', 'Capsule', 'Cylinder', 'Plane', 'Quad'],
          description: 'For create: primitive type to create',
        },
      },
      required: ['action'],
    },
  },

  // 5. unity_component - Component management
  {
    name: 'unity_component',
    description: 'Manage components on GameObjects. Actions: search (find component types), add, set_properties, get_properties, set_property, compare',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['search', 'add', 'set_properties', 'get_properties', 'set_property', 'compare'],
          description: 'Action to perform',
        },
        targetId: {
          type: 'string',
          description: 'Target GameObject ID',
        },
        componentType: {
          type: 'string',
          description: 'Component type name',
        },
        searchQuery: {
          type: 'string',
          description: 'For search: search query',
        },
        properties: {
          type: 'object',
          description: 'For set_properties: property values to set',
        },
        propertyName: {
          type: 'string',
          description: 'For set_property/get_properties: specific property name',
        },
        propertyValue: {
          description: 'For set_property: value to set',
        },
        compareTargetId: {
          type: 'string',
          description: 'For compare: second GameObject ID to compare',
        },
        componentIndex: {
          type: 'number',
          description: 'Component index if multiple of same type',
        },
      },
      required: ['action'],
    },
  },

  // 6. unity_selection - Editor selection
  {
    name: 'unity_selection',
    description: 'Manage editor selection. Actions: get (get current selection), set (set selection)',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get', 'set'],
          description: 'Action to perform',
        },
        targetIds: {
          type: 'array',
          items: { type: 'string' },
          description: 'For set: array of GameObject IDs to select',
        },
        assetPaths: {
          type: 'array',
          items: { type: 'string' },
          description: 'For set: array of asset paths to select',
        },
      },
      required: ['action'],
    },
  },

  // 7. unity_asset - Asset management
  {
    name: 'unity_asset',
    description: 'Manage project assets. Actions: search, create_folder, move, delete, get_info, create_prefab, instantiate_prefab',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['search', 'create_folder', 'move', 'delete', 'get_info', 'create_prefab', 'instantiate_prefab'],
          description: 'Action to perform',
        },
        searchQuery: {
          type: 'string',
          description: 'For search: search filter (e.g., "t:Material", "t:Prefab name")',
        },
        path: {
          type: 'string',
          description: 'Asset path',
        },
        newPath: {
          type: 'string',
          description: 'For move: destination path',
        },
        folderPath: {
          type: 'string',
          description: 'For create_folder: folder path to create',
        },
        targetId: {
          type: 'string',
          description: 'For create_prefab: GameObject ID to make prefab from',
        },
        prefabPath: {
          type: 'string',
          description: 'For instantiate_prefab: prefab asset path',
        },
        parentId: {
          type: 'string',
          description: 'For instantiate_prefab: parent GameObject ID',
        },
      },
      required: ['action'],
    },
  },

  // 8. unity_material - Material management
  {
    name: 'unity_material',
    description: 'Manage materials and shaders. Actions: get_properties, set_property, list, list_shaders',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get_properties', 'set_property', 'list', 'list_shaders'],
          description: 'Action to perform',
        },
        targetId: {
          type: 'string',
          description: 'GameObject ID with Renderer',
        },
        materialPath: {
          type: 'string',
          description: 'Material asset path',
        },
        materialIndex: {
          type: 'number',
          description: 'Material index on renderer',
        },
        propertyName: {
          type: 'string',
          description: 'For set_property: shader property name',
        },
        propertyValue: {
          description: 'For set_property: value to set',
        },
        searchQuery: {
          type: 'string',
          description: 'For list: filter materials by name',
        },
      },
      required: ['action'],
    },
  },

  // 9. unity_prefab - Prefab management
  {
    name: 'unity_prefab',
    description: 'Manage prefabs and overrides. Actions: get_overrides, apply, revert, find_instances',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get_overrides', 'apply', 'revert', 'find_instances'],
          description: 'Action to perform',
        },
        targetId: {
          type: 'string',
          description: 'Prefab instance GameObject ID',
        },
        prefabPath: {
          type: 'string',
          description: 'For find_instances: prefab asset path',
        },
        includeNested: {
          type: 'boolean',
          description: 'Include nested prefab instances',
        },
      },
      required: ['action'],
    },
  },

  // 10. unity_query - Scene queries
  {
    name: 'unity_query',
    description: 'Query scene objects. Actions: find_by_component, find_missing_refs, analyze_layers, find_in_radius',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['find_by_component', 'find_missing_refs', 'analyze_layers', 'find_in_radius'],
          description: 'Action to perform',
        },
        componentType: {
          type: 'string',
          description: 'For find_by_component: component type to search',
        },
        position: {
          type: 'object',
          properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
          description: 'For find_in_radius: center position',
        },
        radius: {
          type: 'number',
          description: 'For find_in_radius: search radius',
        },
        includeInactive: {
          type: 'boolean',
          description: 'Include inactive objects',
        },
      },
      required: ['action'],
    },
  },

  // 11. unity_dependency - Asset dependencies
  {
    name: 'unity_dependency',
    description: 'Analyze asset dependencies. Actions: find_references, get_dependencies, find_unused',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['find_references', 'get_dependencies', 'find_unused'],
          description: 'Action to perform',
        },
        assetPath: {
          type: 'string',
          description: 'Asset path to analyze',
        },
        searchPath: {
          type: 'string',
          description: 'For find_unused: folder to search in',
        },
        assetTypes: {
          type: 'array',
          items: { type: 'string' },
          description: 'For find_unused: asset types to check',
        },
      },
      required: ['action'],
    },
  },

  // 12. unity_animation - Animation control
  {
    name: 'unity_animation',
    description: 'Control animations. Actions: get_state, set_parameter, get_clips, play, sample',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get_state', 'set_parameter', 'get_clips', 'play', 'sample'],
          description: 'Action to perform',
        },
        targetId: {
          type: 'string',
          description: 'GameObject ID with Animator',
        },
        parameterName: {
          type: 'string',
          description: 'For set_parameter: parameter name',
        },
        parameterValue: {
          description: 'For set_parameter: value (bool/int/float/trigger)',
        },
        clipName: {
          type: 'string',
          description: 'For play/sample: animation clip name',
        },
        normalizedTime: {
          type: 'number',
          description: 'For sample: time (0-1)',
        },
        layer: {
          type: 'number',
          description: 'Animator layer index',
        },
      },
      required: ['action'],
    },
  },

  // 13. unity_audio - Audio management
  {
    name: 'unity_audio',
    description: 'Manage audio. Actions: list_sources, get_clip_info, list_clips, preview, set_source',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['list_sources', 'get_clip_info', 'list_clips', 'preview', 'set_source'],
          description: 'Action to perform',
        },
        targetId: {
          type: 'string',
          description: 'GameObject ID with AudioSource',
        },
        clipPath: {
          type: 'string',
          description: 'Audio clip asset path',
        },
        searchPath: {
          type: 'string',
          description: 'For list_clips: folder to search',
        },
        volume: {
          type: 'number',
          description: 'For set_source: volume (0-1)',
        },
        pitch: {
          type: 'number',
          description: 'For set_source: pitch',
        },
        loop: {
          type: 'boolean',
          description: 'For set_source: loop setting',
        },
        play: {
          type: 'boolean',
          description: 'For set_source: start playing',
        },
      },
      required: ['action'],
    },
  },

  // 14. unity_profiler - Performance profiling
  {
    name: 'unity_profiler',
    description: 'Profile performance. Actions: rendering_stats, memory_snapshot, recommendations',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['rendering_stats', 'memory_snapshot', 'recommendations'],
          description: 'Action to perform',
        },
      },
      required: ['action'],
    },
  },

  // 15. unity_screenshot - Capture screenshots
  {
    name: 'unity_screenshot',
    description: 'Capture screenshots. Actions: game_view, scene_view',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['game_view', 'scene_view'],
          description: 'Action to perform',
        },
        width: {
          type: 'number',
          description: 'Screenshot width',
        },
        height: {
          type: 'number',
          description: 'Screenshot height',
        },
        superSize: {
          type: 'number',
          description: 'Super sampling multiplier',
        },
      },
      required: ['action'],
    },
  },

  // 16. unity_xr - XR/VR control
  {
    name: 'unity_xr',
    description: 'Control XR/VR. Actions: set_pose (set rig position/rotation), teleport (teleport to anchor)',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['set_pose', 'teleport'],
          description: 'Action to perform',
        },
        position: {
          type: 'object',
          properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
          description: 'For set_pose: position',
        },
        rotation: {
          type: 'object',
          properties: { x: { type: 'number' }, y: { type: 'number' }, z: { type: 'number' } },
          description: 'For set_pose: euler rotation',
        },
        anchorName: {
          type: 'string',
          description: 'For teleport: anchor name',
        },
      },
      required: ['action'],
    },
  },

  // 17. unity_shadergraph - ShaderGraph management
  {
    name: 'unity_shadergraph',
    description: 'Manage ShaderGraphs. Actions: get_info, list, create, list_node_types',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['get_info', 'list', 'create', 'list_node_types'],
          description: 'Action to perform',
        },
        path: {
          type: 'string',
          description: 'ShaderGraph asset path',
        },
        searchPath: {
          type: 'string',
          description: 'For list: folder to search',
        },
        name: {
          type: 'string',
          description: 'For create: new shader name',
        },
        shaderType: {
          type: 'string',
          enum: ['Lit', 'Unlit', 'Sprite'],
          description: 'For create: shader type',
        },
      },
      required: ['action'],
    },
  },

  // 18. unity_ui - UI inspection
  {
    name: 'unity_ui',
    description: 'Inspect UI. Actions: dump_layout (get UI hierarchy)',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['dump_layout'],
          description: 'Action to perform',
        },
        canvasName: {
          type: 'string',
          description: 'Specific canvas name (optional)',
        },
        includeInactive: {
          type: 'boolean',
          description: 'Include inactive UI elements',
        },
      },
      required: ['action'],
    },
  },

  // 19. unity_menu - Menu item execution
  {
    name: 'unity_menu',
    description: 'Execute Unity menu items. Actions: execute (run menu item), list (list available items)',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['execute', 'list'],
          description: 'Action to perform',
        },
        menuPath: {
          type: 'string',
          description: 'For execute: menu path (e.g., "GameObject/Create Empty")',
        },
        searchPath: {
          type: 'string',
          description: 'For list: filter menu items',
        },
      },
      required: ['action'],
    },
  },

  // 20. unity_code - Code execution
  {
    name: 'unity_code',
    description: 'Execute C# code in Unity. Actions: execute (run code block), evaluate (evaluate expression)',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['execute', 'evaluate'],
          description: 'Action to perform',
        },
        code: {
          type: 'string',
          description: 'C# code to execute',
        },
        expression: {
          type: 'string',
          description: 'For evaluate: C# expression to evaluate',
        },
      },
      required: ['action'],
    },
  },

  // 21. unity_test - Test runner
  {
    name: 'unity_test',
    description: 'Run Unity tests. Actions: run',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['run'],
          description: 'Action to perform',
        },
        testMode: {
          type: 'string',
          enum: ['EditMode', 'PlayMode', 'All'],
          description: 'Test mode to run',
        },
        testFilter: {
          type: 'string',
          description: 'Filter tests by name',
        },
        categories: {
          type: 'array',
          items: { type: 'string' },
          description: 'Test categories to include',
        },
      },
      required: ['action'],
    },
  },

  // 22. unity_build - Build player
  {
    name: 'unity_build',
    description: 'Build Unity player. Actions: player',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['player'],
          description: 'Action to perform',
        },
        target: {
          type: 'string',
          enum: ['StandaloneWindows64', 'StandaloneOSX', 'StandaloneLinux64', 'Android', 'iOS', 'WebGL'],
          description: 'Build target platform',
        },
        outputPath: {
          type: 'string',
          description: 'Output path for build',
        },
        scenes: {
          type: 'array',
          items: { type: 'string' },
          description: 'Scene paths to include',
        },
        developmentBuild: {
          type: 'boolean',
          description: 'Enable development build',
        },
      },
      required: ['action'],
    },
  },

  // 23. unity_project - Project management
  {
    name: 'unity_project',
    description: 'Manage Unity projects. Actions: list (list registered projects), switch (switch active project), get_active',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['list', 'switch', 'get_active'],
          description: 'Action to perform',
        },
        projectPath: {
          type: 'string',
          description: 'For switch: project path to switch to',
        },
        includeInactive: {
          type: 'boolean',
          description: 'For list: include inactive projects',
        },
      },
      required: ['action'],
    },
  },

  // 24. unity_batch - Batch operations
  {
    name: 'unity_batch',
    description: 'Execute multiple operations in a batch. Actions: execute',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['execute'],
          description: 'Action to perform',
        },
        operations: {
          type: 'array',
          items: {
            type: 'object',
            properties: {
              tool: { type: 'string' },
              action: { type: 'string' },
              params: { type: 'object' },
            },
          },
          description: 'Array of operations to execute',
        },
        stopOnError: {
          type: 'boolean',
          description: 'Stop batch on first error',
        },
      },
      required: ['action'],
    },
  },

  // 25. unity_script - Script management (Phase 41)
  scriptToolDefinition,

  // 26. unity_package - Package manager (Phase 42)
  packageToolDefinition,

  // 27. unity_status - Connection status (always works, even without Unity)
  {
    name: 'unity_status',
    description: 'Check Unity connection status. This tool always works, even when Unity is not connected. Use this to check if Unity is ready before calling other tools.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        action: {
          type: 'string',
          enum: ['check'],
          description: 'Action to perform (check)',
        },
      },
      required: ['action'],
    },
  },
];

// ============================================================================
// Consolidated Tool Handlers
// ============================================================================

type ToolParams = Record<string, unknown>;

export const consolidatedToolHandlers: Record<string, (params: ToolParams) => Promise<unknown>> = {
  // 1. unity_editor
  unity_editor: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get_state': return getEditorState(rest);
      case 'set_play_mode': return setPlayMode(rest as any);
      case 'get_context': return getActiveContext(rest as any);
      case 'recompile': return recompileScripts();
      case 'refresh': return refreshAssets();
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 2. unity_console
  unity_console: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get_logs': return getConsoleLogs(rest);
      case 'clear': return clearConsoleLogs(rest);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 3. unity_scene
  unity_scene: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'list': return listScenes(rest);
      case 'hierarchy': return getSceneHierarchy(rest);
      case 'create': return createScene(rest as any);
      case 'load': return loadScene(rest as any);
      case 'save': return saveScene(rest as any);
      case 'delete': return deleteScene(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 4. unity_gameobject
  unity_gameobject: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'create': return createGameObject(rest as any);
      case 'modify': return modifyGameObject(rest as any);
      case 'delete': return deleteGameObject(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 5. unity_component
  unity_component: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'search': return searchComponentTypes(rest as any);
      case 'add': return addComponent(rest as any);
      case 'set_properties': return setComponentProperties(rest as any);
      case 'get_properties': return getComponentProperties(rest as any);
      case 'set_property': return setComponentProperty(rest as any);
      case 'compare': return compareComponents(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 6. unity_selection
  unity_selection: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get': return getEditorSelection();
      case 'set': return setEditorSelection(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 7. unity_asset
  unity_asset: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'search': return searchAssets(rest as any);
      case 'create_folder': return createFolder(rest as any);
      case 'move': return moveAsset(rest as any);
      case 'delete': return deleteAsset(rest as any);
      case 'get_info': return getAssetInfo(rest as any);
      case 'create_prefab': return createPrefab(rest as any);
      case 'instantiate_prefab': return instantiatePrefab(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 8. unity_material
  unity_material: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get_properties': return getMaterialProperties(rest as any);
      case 'set_property': return setMaterialProperty(rest as any);
      case 'list': return listMaterials(rest as any);
      case 'list_shaders': return listShaders();
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 9. unity_prefab
  unity_prefab: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get_overrides': return getPrefabOverrides(rest as any);
      case 'apply': return applyPrefabOverrides(rest as any);
      case 'revert': return revertPrefabOverrides(rest as any);
      case 'find_instances': return findPrefabInstances(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 10. unity_query
  unity_query: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'find_by_component': return findObjectsWithComponent(rest as any);
      case 'find_missing_refs': return findMissingReferences();
      case 'analyze_layers': return analyzeLayers();
      case 'find_in_radius': return findObjectsInRadius(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 11. unity_dependency
  unity_dependency: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'find_references': return findAssetReferences(rest as any);
      case 'get_dependencies': return getAssetDependencies(rest as any);
      case 'find_unused': return findUnusedAssets(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 12. unity_animation
  unity_animation: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get_state': return getAnimatorState(rest as any);
      case 'set_parameter': return setAnimatorParameter(rest as any);
      case 'get_clips': return getAnimationClips(rest as any);
      case 'play': return playAnimation(rest as any);
      case 'sample': return sampleAnimation(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 13. unity_audio
  unity_audio: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'list_sources': return listAudioSources();
      case 'get_clip_info': return getAudioClipInfo(rest as any);
      case 'list_clips': return listAudioClips(rest as any);
      case 'preview': return previewAudio(rest as any);
      case 'set_source': return setAudioSource(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 14. unity_profiler
  unity_profiler: async (params) => {
    const { action } = params;
    switch (action) {
      case 'rendering_stats': return getRenderingStats();
      case 'memory_snapshot': return getMemorySnapshot();
      case 'recommendations': return getPerformanceRecommendations();
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 15. unity_screenshot
  unity_screenshot: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'game_view': return captureGameViewScreenshot(rest);
      case 'scene_view': return captureSceneViewScreenshot(rest);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 16. unity_xr
  unity_xr: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'set_pose': return setXRRigPose(rest as any);
      case 'teleport': return teleportXRRigToAnchor(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 17. unity_shadergraph
  unity_shadergraph: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'get_info': return shaderGraphToolHandlers.get_shadergraph_info(rest);
      case 'list': return shaderGraphToolHandlers.list_shadergraphs(rest);
      case 'create': return shaderGraphToolHandlers.create_shadergraph(rest);
      case 'list_node_types': return shaderGraphToolHandlers.list_shadergraph_node_types(rest);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 18. unity_ui
  unity_ui: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'dump_layout': return dumpUILayout(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 19. unity_menu
  unity_menu: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'execute': return executeMenuItem(rest as any);
      case 'list': return listMenuItems(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 20. unity_code
  unity_code: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'execute': return executeCode(rest as any);
      case 'evaluate': return evaluateExpression(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 21. unity_test
  unity_test: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'run': return runTests(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 22. unity_build
  unity_build: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'player': return buildPlayer(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 23. unity_project
  unity_project: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'list': return handleListUnityProjects(rest as any);
      case 'switch': return handleSwitchProject(rest as any);
      case 'get_active': return handleGetActiveProject();
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 24. unity_batch
  unity_batch: async (params) => {
    const { action, ...rest } = params;
    switch (action) {
      case 'execute': return batchExecute(rest as any);
      default: throw new Error(`Unknown action: ${action}`);
    }
  },

  // 25. unity_script - Script management (Phase 41)
  unity_script: async (params) => {
    const result = await executeScriptTool(params);
    // executeScriptTool returns { content, isError }, extract the text
    if (result.content && result.content[0]) {
      return JSON.parse(result.content[0].text);
    }
    return result;
  },

  // 26. unity_package - Package manager (Phase 42)
  unity_package: async (params) => {
    const result = await executePackageTool(params);
    if (result.content && result.content[0]) {
      return JSON.parse(result.content[0].text);
    }
    return result;
  },

  // 27. unity_status - Connection status (always works, even without Unity)
  unity_status: async (params) => {
    // Import dynamically to avoid circular dependency
    const { getWebSocketHub } = await import('../websocketHub.js');
    const hub = getWebSocketHub();
    const status = hub.getStatus() as {
      running: boolean;
      port: number;
      sessions: Array<{
        sessionId: string;
        projectName: string;
        unityVersion: string;
        connectedAt: string;
      }>;
    };
    
    const isConnected = status.sessions && status.sessions.length > 0;
    const isServerRunning = status.running;
    
    // Determine overall status
    let overallStatus: string;
    if (!isServerRunning) {
      overallStatus = 'secondary_instance';
    } else if (isConnected) {
      overallStatus = 'connected';
    } else {
      overallStatus = 'waiting_for_unity';
    }
    
    return {
      status: overallStatus,
      server: {
        running: isServerRunning,
        port: status.port,
        version: '1.1.0',
        mode: isServerRunning ? 'primary' : 'secondary',
      },
      unity: isConnected ? {
        connected: true,
        sessions: status.sessions.map(s => ({
          projectName: s.projectName,
          unityVersion: s.unityVersion,
          connectedAt: s.connectedAt,
        })),
      } : {
        connected: false,
        message: isServerRunning 
          ? 'No Unity Editor is connected. Open Unity with the UnityVision package installed.'
          : 'This is a secondary MCP instance. Unity should connect to the primary instance.',
        help: isServerRunning ? [
          '1. Open Unity Editor',
          '2. Ensure UnityVision package is installed',
          '3. Check Window > UnityVision > Bridge Status',
          '4. Unity will connect automatically',
        ] : [
          'Another Windsurf instance is already running the primary MCP server.',
          'Unity projects will connect to that instance instead.',
          'You can still use this Windsurf for other tasks.',
          'Close other Windsurf instances if you want this one to be primary.',
        ],
      },
    };
  },
};
