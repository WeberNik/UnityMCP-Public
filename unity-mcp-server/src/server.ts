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
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';

import { allToolDefinitions, executeTool } from './tools/index.js';
import { getResourceList, fetchResource } from './resources/index.js';
import { startWebSocketHub, getWebSocketHub } from './websocketHub.js';
import { fileLog } from './fileLogger.js';

// Lock file to prevent multiple instances
const LOCK_FILE = path.join(os.tmpdir(), 'unityvision-mcp.lock');

function acquireLock(): boolean {
  try {
    // Check if lock file exists and if the process is still running
    if (fs.existsSync(LOCK_FILE)) {
      const lockData = fs.readFileSync(LOCK_FILE, 'utf8');
      const pid = parseInt(lockData, 10);
      
      // Check if process is still running
      try {
        process.kill(pid, 0); // Signal 0 just checks if process exists
        // Process exists - another instance is running
        fileLog('WARN', 'Server', `Another instance running (PID ${pid}), this instance will exit`);
        return false;
      } catch {
        // Process doesn't exist - stale lock file, remove it
        fs.unlinkSync(LOCK_FILE);
      }
    }
    
    // Create lock file with our PID
    fs.writeFileSync(LOCK_FILE, process.pid.toString());
    return true;
  } catch (error) {
    fileLog('ERROR', 'Server', `Lock file error: ${(error as Error).message}`);
    return true; // Allow running if lock mechanism fails
  }
}

function releaseLock(): void {
  try {
    if (fs.existsSync(LOCK_FILE)) {
      const lockData = fs.readFileSync(LOCK_FILE, 'utf8');
      const pid = parseInt(lockData, 10);
      // Only remove if it's our lock
      if (pid === process.pid) {
        fs.unlinkSync(LOCK_FILE);
      }
    }
  } catch {
    // Ignore errors during cleanup
  }
}

// Server metadata
const SERVER_NAME = 'unity-vision';
const SERVER_VERSION = '1.1.0';

async function main() {
  // Try to acquire lock - if another instance is running, exit immediately
  if (!acquireLock()) {
    console.error('[UnityVision] Another instance is already running. Exiting.');
    process.exit(0);
  }
  
  // Get WebSocket port from environment or use default
  const wsPort = parseInt(process.env.UNITY_VISION_WS_PORT || '6400', 10);
  
  fileLog('INFO', 'Server', `Starting UnityVision MCP server (PID ${process.pid})...`);
  console.error('[UnityVision] Starting MCP server...');
  
  // Start WebSocket hub for Unity connections
  console.error(`[UnityVision] Starting WebSocket hub on port ${wsPort}...`);
  const hub = await startWebSocketHub(wsPort);
  const actualPort = hub.getPort();
  
  fileLog('INFO', 'Server', `WebSocket hub started on port ${actualPort}`);
  console.error(`[UnityVision] WebSocket hub ready - Unity can connect to ws://localhost:${actualPort}`);

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
  
  // Cleanup function to ensure proper shutdown
  const cleanup = (reason: string, exitCode: number = 0) => {
    console.error(`[UnityVision] Shutting down (${reason})...`);
    fileLog('INFO', 'Server', `Shutting down (${reason})`);
    hub.stop();
    releaseLock();
    process.exit(exitCode);
  };
  
  // Handle graceful shutdown
  process.on('SIGINT', () => cleanup('SIGINT'));
  process.on('SIGTERM', () => cleanup('SIGTERM'));
  
  // Handle uncaught exceptions to ensure cleanup
  process.on('uncaughtException', (error) => {
    console.error('[UnityVision] Uncaught exception:', error);
    fileLog('ERROR', 'Server', `Uncaught exception: ${error.message}`);
    cleanup('uncaughtException', 1);
  });
  
  // Handle unhandled promise rejections
  process.on('unhandledRejection', (reason, promise) => {
    console.error('[UnityVision] Unhandled rejection:', reason);
    fileLog('ERROR', 'Server', `Unhandled rejection: ${reason}`);
  });
  
  // Handle stdin close (parent process died) - critical for MCP cleanup
  process.stdin.on('close', () => cleanup('stdin close'));
  process.stdin.on('end', () => cleanup('stdin end'));
  
  // Handle process exit to ensure lock is released
  process.on('exit', () => {
    releaseLock();
  });
  
  // Also handle beforeExit for async cleanup
  process.on('beforeExit', () => {
    releaseLock();
  });
}

// Run the server
main().catch((error) => {
  console.error('[UnityVision] Fatal error:', error);
  releaseLock();
  process.exit(1);
});
