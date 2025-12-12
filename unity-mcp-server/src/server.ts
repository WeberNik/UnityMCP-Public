#!/usr/bin/env node
// ============================================================================
// UnityVision MCP Server - Main Entry Point
// MCP server for Unity Editor integration with Windsurf
// Uses WebSocket for persistent, reliable Unity communication
// ============================================================================

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ListResourcesRequestSchema,
  ReadResourceRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';

import { allToolDefinitions, executeTool } from './tools/index.js';
import { getResourceList, fetchResource } from './resources/index.js';
import { startWebSocketHub, getWebSocketHub } from './websocketHub.js';
import { fileLog } from './fileLogger.js';

// Server metadata
const SERVER_NAME = 'unity-vision';
const SERVER_VERSION = '1.1.0';

async function main() {
  // Get WebSocket port from environment or use default
  const wsPort = parseInt(process.env.UNITY_VISION_WS_PORT || '7890', 10);
  
  fileLog('INFO', 'Server', 'Starting UnityVision MCP server...');
  console.error('[UnityVision] Starting MCP server...');
  
  // Start WebSocket hub for Unity connections
  console.error(`[UnityVision] Starting WebSocket hub on port ${wsPort}...`);
  const hub = await startWebSocketHub(wsPort);
  
  fileLog('INFO', 'Server', `WebSocket hub started on port ${wsPort}`);
  console.error(`[UnityVision] WebSocket hub ready - Unity can connect to ws://localhost:${wsPort}`);

  // Create MCP server
  const server = new Server(
    {
      name: SERVER_NAME,
      version: SERVER_VERSION,
    },
    {
      capabilities: {
        tools: {},
        resources: {},
      },
    }
  );

  // Handle tool listing
  server.setRequestHandler(ListToolsRequestSchema, async () => {
    return {
      tools: allToolDefinitions,
    };
  });

  // Handle resource listing
  server.setRequestHandler(ListResourcesRequestSchema, async () => {
    fileLog('INFO', 'Server', 'Listing resources');
    return {
      resources: getResourceList(),
    };
  });

  // Handle resource reading
  server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
    const { uri } = request.params;
    fileLog('INFO', 'Server', `Reading resource: ${uri}`);
    
    try {
      const content = await fetchResource(uri);
      return {
        contents: [
          {
            uri,
            mimeType: 'application/json',
            text: content,
          },
        ],
      };
    } catch (error) {
      throw new Error(`Failed to read resource ${uri}: ${(error as Error).message}`);
    }
  });

  // Handle tool execution
  server.setRequestHandler(CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;

    // Validate tool exists
    const toolDef = allToolDefinitions.find((t) => t.name === name);
    if (!toolDef) {
      throw new Error(`Unknown tool: ${name}`);
    }

    // Log tool execution
    fileLog('INFO', 'Server', `Executing tool: ${name}`);

    // Execute the tool
    const result = await executeTool(name, args ?? {});
    return result;
  });

  // Start the server with stdio transport
  const transport = new StdioServerTransport();
  await server.connect(transport);

  console.error(`[UnityVision] MCP server started (${SERVER_NAME} v${SERVER_VERSION})`);
  console.error('[UnityVision] Waiting for Unity to connect...');
  console.error('[UnityVision] Make sure Unity Editor is running with the UnityVision package installed.');
  
  fileLog('INFO', 'Server', 'MCP server ready');
  
  // Handle graceful shutdown
  process.on('SIGINT', () => {
    console.error('[UnityVision] Shutting down...');
    fileLog('INFO', 'Server', 'Shutting down (SIGINT)');
    hub.stop();
    process.exit(0);
  });
  
  process.on('SIGTERM', () => {
    console.error('[UnityVision] Shutting down...');
    fileLog('INFO', 'Server', 'Shutting down (SIGTERM)');
    hub.stop();
    process.exit(0);
  });
}

// Run the server
main().catch((error) => {
  console.error('[UnityVision] Fatal error:', error);
  process.exit(1);
});
