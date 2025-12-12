// ============================================================================
// UnityVision MCP Server - Component Tools
// Tools for adding components and setting properties
// ============================================================================

import { getBridgeClient } from '../unityBridgeClient.js';
import {
  AddComponentInput,
  AddComponentOutput,
  SetComponentPropertiesInput,
  SetComponentPropertiesOutput,
  SearchComponentTypesInput,
  SearchComponentTypesOutput,
} from '../types.js';

export const componentToolDefinitions = [
  {
    name: 'search_component_types',
    description: 'Search for available component types by name. Use this to find the correct fully-qualified type name before adding a component.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        query: {
          type: 'string',
          description: 'Search query to match against component type names (e.g., "Rigidbody", "Collider", "Light").',
        },
        maxResults: {
          type: 'number',
          description: 'Maximum number of results to return. Default is 50.',
          default: 50,
        },
        includeUnityEngine: {
          type: 'boolean',
          description: 'Include UnityEngine components. Default is true.',
          default: true,
        },
        includeUnityEditor: {
          type: 'boolean',
          description: 'Include UnityEditor components. Default is false.',
          default: false,
        },
        includeUserAssemblies: {
          type: 'boolean',
          description: 'Include user/project components. Default is true.',
          default: true,
        },
      },
      required: [] as string[],
    },
  },
  {
    name: 'add_component',
    description: 'Add a component to a GameObject by type name. Supports dry-run mode.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the GameObject to add the component to.',
        },
        componentType: {
          type: 'string',
          description: 'Full type name of the component (e.g., "UnityEngine.UI.Button", "MyNamespace.MyComponent").',
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns a plan without actually adding the component.',
          default: false,
        },
      },
      required: ['gameObjectPath', 'componentType'],
    },
  },
  {
    name: 'set_component_properties',
    description: 'Set serialized properties on a component. Uses Unity serialization and reflection to set field values.',
    inputSchema: {
      type: 'object' as const,
      properties: {
        gameObjectPath: {
          type: 'string',
          description: 'Path to the GameObject containing the component.',
        },
        componentType: {
          type: 'string',
          description: 'Type name of the component to modify.',
        },
        properties: {
          type: 'object',
          description: 'Key-value pairs of property names and values to set. Values can be primitives, strings, or paths to other GameObjects for references.',
          additionalProperties: true,
        },
        dryRun: {
          type: 'boolean',
          description: 'If true, returns a plan without actually modifying properties.',
          default: false,
        },
      },
      required: ['gameObjectPath', 'componentType', 'properties'],
    },
  },
];

export async function addComponent(
  params: AddComponentInput
): Promise<AddComponentOutput> {
  const client = getBridgeClient();
  return client.call<AddComponentInput, AddComponentOutput>(
    'add_component',
    {
      gameObjectPath: params.gameObjectPath,
      componentType: params.componentType,
      dryRun: params.dryRun ?? false,
    }
  );
}

export async function setComponentProperties(
  params: SetComponentPropertiesInput
): Promise<SetComponentPropertiesOutput> {
  const client = getBridgeClient();
  return client.call<SetComponentPropertiesInput, SetComponentPropertiesOutput>(
    'set_component_properties',
    {
      gameObjectPath: params.gameObjectPath,
      componentType: params.componentType,
      properties: params.properties,
      dryRun: params.dryRun ?? false,
    }
  );
}

export async function searchComponentTypes(
  params: SearchComponentTypesInput
): Promise<SearchComponentTypesOutput> {
  const client = getBridgeClient();
  return client.call<SearchComponentTypesInput, SearchComponentTypesOutput>(
    'search_component_types',
    {
      query: params.query ?? '',
      maxResults: params.maxResults ?? 50,
      includeUnityEngine: params.includeUnityEngine ?? true,
      includeUnityEditor: params.includeUnityEditor ?? false,
      includeUserAssemblies: params.includeUserAssemblies ?? true,
    }
  );
}
