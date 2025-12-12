// ============================================================================
// UnityVision MCP Server - Profiler Tools
// Tools for performance analysis and optimization
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';

export const profilerToolDefinitions = [
  {
    name: 'get_rendering_stats',
    description: `Get current rendering statistics and performance metrics.

**Returns:**
- Draw calls, batches, triangles, vertices
- Memory usage (total, graphics, managed)
- Screen resolution, quality settings
- Render pipeline info

**Use Cases:**
- "How many draw calls are there?"
- "What's the memory usage?"
- Quick performance health check`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'get_memory_snapshot',
    description: `Get detailed memory usage breakdown.

**Returns:**
- Memory by category (graphics, managed, native)
- Largest allocations (textures, meshes)
- Formatted sizes

**Use Cases:**
- "What's using the most memory?"
- "Which textures are largest?"
- Memory optimization`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
  {
    name: 'get_performance_recommendations',
    description: `Analyze the scene and get performance optimization suggestions.

**Checks:**
- Draw call count
- Realtime lights and shadows
- Triangle/vertex count
- Memory usage
- Camera count
- UI canvas setup

**Returns:**
- List of issues with severity
- Specific recommendations
- Overall health rating`,
    inputSchema: {
      type: 'object' as const,
      properties: {},
      required: [] as string[],
    },
  },
];

export async function getRenderingStats(): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_rendering_stats', {});
}

export async function getMemorySnapshot(): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_memory_snapshot', {});
}

export async function getPerformanceRecommendations(): Promise<any> {
  const client = getBridgeClient();
  return client.call('get_performance_recommendations', {});
}
