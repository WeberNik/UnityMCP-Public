// ============================================================================
// UnityVision MCP Server - Type Definitions
// ============================================================================

// --- Common Types ---

export interface Vector3 {
  x: number;
  y: number;
  z: number;
}

export interface Vector2 {
  x: number;
  y: number;
}

export interface TransformData {
  position?: Vector3;
  rotation?: Vector3;
  scale?: Vector3;
}

// --- Editor State Types ---

export interface GetEditorStateInput {
  // No parameters required
}

export interface GetEditorStateOutput {
  unityVersion: string;
  projectPath: string;
  isPlaying: boolean;
  isPaused: boolean;
  activeScene: string;
  loadedScenes: string[];
  platform: string;
}

export interface SetPlayModeInput {
  mode: 'play' | 'pause' | 'stop';
}

export interface SetPlayModeOutput {
  success: boolean;
  previousMode: string;
  currentMode: string;
}

// --- Console Log Types ---

export interface GetConsoleLogsInput {
  level?: 'error' | 'warning' | 'info' | 'all';
  maxEntries?: number;
  includeStackTrace?: boolean;
  sinceTimeMs?: number;
}

export interface LogEntry {
  timestamp: number;
  type: 'Error' | 'Warning' | 'Log' | 'Exception' | 'Assert';
  message: string;
  stackTrace?: string;
}

export interface GetConsoleLogsOutput {
  entries: LogEntry[];
}

export interface ClearConsoleLogsInput {
  // No parameters required
}

export interface ClearConsoleLogsOutput {
  success: boolean;
}

// --- Scene Types ---

export interface ListScenesInput {
  filter?: string;
}

export interface SceneInfo {
  name: string;
  path: string;
  isLoaded: boolean;
  isActive: boolean;
  buildIndex: number;
}

export interface ListScenesOutput {
  scenes: SceneInfo[];
}

export interface GetSceneHierarchyInput {
  sceneName?: string;
  rootPath?: string;
  maxDepth?: number;
  includeComponents?: boolean;
  nameFilter?: string;
  maxObjects?: number;
}

export interface GameObjectNode {
  id: string;
  name: string;
  path: string;
  active: boolean;
  components?: string[];
  children?: GameObjectNode[];
}

export interface GetSceneHierarchyOutput {
  rootObjects: GameObjectNode[];
  totalObjectsInScene?: number;
  returnedObjects?: number;
  truncated?: boolean;
}

// --- GameObject Types ---

export interface ComponentSpec {
  type: string;
  properties?: Record<string, unknown>;
}

export interface CreateGameObjectInput {
  sceneName?: string;
  parentPath?: string;
  name: string;
  components?: ComponentSpec[];
  position?: Vector3;
  rotation?: Vector3;
  scale?: Vector3;
  dryRun?: boolean;
}

export interface CreateGameObjectOutput {
  success: boolean;
  id?: string;
  path?: string;
  dryRunPlan?: {
    wouldCreate: string;
    at: string;
    withComponents: string[];
  };
}

export interface ModifyGameObjectInput {
  path: string;
  newName?: string;
  parentPath?: string | null;
  transform?: TransformData;
  active?: boolean;
  dryRun?: boolean;
}

export interface ModifyGameObjectOutput {
  success: boolean;
  dryRunPlan?: {
    wouldModify: string;
    changes: Record<string, unknown>;
  };
}

export interface DeleteGameObjectInput {
  path: string;
  confirm?: boolean;
  dryRun?: boolean;
}

export interface DeleteGameObjectOutput {
  success: boolean;
  dryRunPlan?: {
    wouldDelete: string;
    childCount: number;
  };
}

// --- Component Types ---

export interface AddComponentInput {
  gameObjectPath: string;
  componentType: string;
  dryRun?: boolean;
}

export interface AddComponentOutput {
  success: boolean;
  componentId?: string;
  dryRunPlan?: {
    wouldAdd: string;
    to: string;
  };
}

export interface SetComponentPropertiesInput {
  gameObjectPath: string;
  componentType: string;
  properties: Record<string, unknown>;
  dryRun?: boolean;
}

export interface SetComponentPropertiesOutput {
  success: boolean;
  modifiedProperties?: string[];
  dryRunPlan?: {
    wouldModify: string;
    properties: Record<string, unknown>;
  };
}

// --- UI Layout Types ---

export interface DumpUILayoutInput {
  rootCanvasPath: string;
  maxDepth?: number;
  includeInactive?: boolean;
}

export interface RectTransformData {
  anchoredPosition: Vector2;
  sizeDelta: Vector2;
  anchorMin: Vector2;
  anchorMax: Vector2;
  pivot: Vector2;
}

export interface UIElementNode {
  name: string;
  path: string;
  active: boolean;
  rect: RectTransformData;
  components?: string[];
  children?: UIElementNode[];
}

export interface DumpUILayoutOutput {
  root: UIElementNode;
}

// --- Screenshot Types ---

export interface CameraInfo {
  position: number[];
  rotation: number[];
  fieldOfView: number;
  isOrthographic: boolean;
  orthographicSize: number;
}

export interface CaptureGameViewScreenshotInput {
  resolutionWidth?: number;
  resolutionHeight?: number;
  superSampling?: number;
  camera?: string;
  includeGizmos?: boolean;
  format?: 'png_base64' | 'jpg_base64';
  /** If true and no camera exists, create a temporary camera matching Scene View. Default: true */
  createFromSceneView?: boolean;
}

export interface CaptureGameViewScreenshotOutput {
  success: boolean;
  imageFormat: string;
  imageData: string;
  width: number;
  height: number;
  /** Indicates which camera was used: "main_camera", "specified_camera", "first_available_camera", "scene_view_temp" */
  cameraSource?: string;
  cameraInfo?: CameraInfo;
}

export interface CaptureSceneViewScreenshotInput {
  resolutionWidth?: number;
  resolutionHeight?: number;
  focusObjectPath?: string;
  cameraPosition?: Vector3;
  cameraRotation?: Vector3;
  format?: 'png_base64' | 'jpg_base64';
  /** If true, attempts to capture exactly what the user sees including grid and gizmos. Default: true */
  captureEditorView?: boolean;
}

export interface CaptureSceneViewScreenshotOutput {
  success: boolean;
  imageFormat: string;
  imageData: string;
  width: number;
  height: number;
  cameraSource?: string;
  cameraInfo?: CameraInfo;
}

// --- XR Types ---

export interface SetXRRigPoseInput {
  rigRootPath: string;
  position: Vector3;
  rotationEuler: Vector3;
  dryRun?: boolean;
}

export interface SetXRRigPoseOutput {
  success: boolean;
  dryRunPlan?: {
    wouldMove: string;
    to: Vector3;
    rotation: Vector3;
  };
}

export interface TeleportXRRigToAnchorInput {
  rigRootPath: string;
  anchorObjectPath: string;
  dryRun?: boolean;
}

export interface TeleportXRRigToAnchorOutput {
  success: boolean;
  dryRunPlan?: {
    wouldTeleport: string;
    to: string;
    position: Vector3;
  };
}

// --- Test Types ---

export interface RunTestsInput {
  testMode: 'EditMode' | 'PlayMode' | 'Both';
  filter?: string;
  timeout?: number;
}

export interface TestResult {
  name: string;
  passed: boolean;
  message?: string;
  stackTrace?: string;
  duration?: number;
}

export interface RunTestsOutput {
  success: boolean;
  summary: {
    total: number;
    passed: number;
    failed: number;
    ignored: number;
    duration: number;
  };
  failedTests: TestResult[];
}

// --- Build Types ---

export interface BuildPlayerInput {
  targetPlatform: 'Android' | 'iOS' | 'StandaloneWindows64' | 'StandaloneOSX';
  buildPath?: string;
  developmentBuild?: boolean;
  buildOptions?: string[];
  scenes?: string[];
}

export interface BuildPlayerOutput {
  success: boolean;
  buildPath?: string;
  duration?: number;
  errors?: string[];
  warnings?: string[];
}

// --- Bridge Communication Types ---

export interface BridgeRequest {
  method: string;
  params: unknown;
}

export interface BridgeResponse<T = unknown> {
  result?: T;
  error?: {
    code: string;
    message: string;
    details?: Record<string, unknown>;
  };
}

// --- Search Component Types ---

export interface SearchComponentTypesInput {
  query?: string;
  maxResults?: number;
  includeUnityEngine?: boolean;
  includeUnityEditor?: boolean;
  includeUserAssemblies?: boolean;
}

export interface ComponentTypeInfo {
  fullName: string;
  shortName: string;
  assemblyName: string;
  namespace: string;
}

export interface SearchComponentTypesOutput {
  results: ComponentTypeInfo[];
  totalMatches: number;
}

// --- Active Context Types ---

export interface GetActiveContextInput {
  maxConsoleErrors?: number;
  includeSelection?: boolean;
  includePlayModeState?: boolean;
}

export interface SelectedObjectInfo {
  name: string;
  path: string;
  type: string;
  components?: string[];
}

export interface ConsoleErrorInfo {
  timestamp: number;
  type: string;
  message: string;
}

export interface GetActiveContextOutput {
  playModeState?: string;
  isCompiling?: boolean;
  activeScene?: string;
  selectedObject?: SelectedObjectInfo;
  selectedObjects?: SelectedObjectInfo[];
  recentErrors?: ConsoleErrorInfo[];
}

// --- Error Types ---

export class UnityBridgeError extends Error {
  constructor(
    public code: string,
    message: string,
    public details?: Record<string, unknown>
  ) {
    super(message);
    this.name = 'UnityBridgeError';
  }
}
