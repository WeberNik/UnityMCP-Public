#!/usr/bin/env node
// ============================================================================
// UnityVision MCP - Command Line Interface
// Provides setup, status, and project management commands
// ============================================================================

import { spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as readline from 'readline';

// ANSI color codes
const colors = {
  reset: '\x1b[0m',
  bright: '\x1b[1m',
  dim: '\x1b[2m',
  green: '\x1b[32m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  red: '\x1b[31m',
  cyan: '\x1b[36m',
};

const REGISTRY_PATH = path.join(os.homedir(), '.unityvision', 'projects.json');

interface ProjectEntry {
  projectPath: string;
  projectName: string;
  port: number;
  pid: number;
  unityVersion: string;
  lastSeen: string;
  isActive: boolean;
}

interface AIClientConfig {
  name: string;
  configPath: string;
  configKey: string;
}

// Detect AI client config locations
function getAIClients(): AIClientConfig[] {
  const home = os.homedir();
  const clients: AIClientConfig[] = [];

  // Windsurf
  const windsurfPaths = [
    path.join(home, '.codeium', 'windsurf', 'mcp_config.json'),
    path.join(home, 'AppData', 'Roaming', 'Windsurf', 'mcp_config.json'),
  ];
  for (const p of windsurfPaths) {
    if (fs.existsSync(p) || fs.existsSync(path.dirname(p))) {
      clients.push({ name: 'Windsurf', configPath: p, configKey: 'mcpServers' });
      break;
    }
  }

  // Claude Desktop
  const claudePaths = [
    path.join(home, 'AppData', 'Roaming', 'Claude', 'claude_desktop_config.json'),
    path.join(home, 'Library', 'Application Support', 'Claude', 'claude_desktop_config.json'),
    path.join(home, '.config', 'claude', 'claude_desktop_config.json'),
  ];
  for (const p of claudePaths) {
    if (fs.existsSync(p) || fs.existsSync(path.dirname(p))) {
      clients.push({ name: 'Claude Desktop', configPath: p, configKey: 'mcpServers' });
      break;
    }
  }

  // Cursor
  const cursorPaths = [
    path.join(home, '.cursor', 'mcp.json'),
    path.join(home, 'AppData', 'Roaming', 'Cursor', 'mcp.json'),
  ];
  for (const p of cursorPaths) {
    if (fs.existsSync(p) || fs.existsSync(path.dirname(p))) {
      clients.push({ name: 'Cursor', configPath: p, configKey: 'mcpServers' });
      break;
    }
  }

  return clients;
}

// Read project registry
function readRegistry(): ProjectEntry[] {
  try {
    if (fs.existsSync(REGISTRY_PATH)) {
      const content = fs.readFileSync(REGISTRY_PATH, 'utf-8');
      return JSON.parse(content);
    }
  } catch (e) {
    // Ignore errors
  }
  return [];
}

// Check if a project is reachable
async function checkProjectHealth(port: number): Promise<boolean> {
  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 2000);
    
    const response = await fetch(`http://localhost:${port}/health`, {
      signal: controller.signal,
    });
    clearTimeout(timeout);
    return response.ok;
  } catch {
    return false;
  }
}

// Print styled header
function printHeader(): void {
  console.log(`
${colors.cyan}${colors.bright}╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║   🎮  UnityVision MCP - AI Bridge for Unity Editor        ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝${colors.reset}
`);
}

// Print help
function printHelp(): void {
  printHeader();
  console.log(`${colors.bright}USAGE${colors.reset}
  unityvision-mcp <command> [options]

${colors.bright}COMMANDS${colors.reset}
  ${colors.green}start${colors.reset}              Start the MCP server (stdio mode)
  ${colors.green}setup${colors.reset} [--client]   Configure an AI client to use UnityVision
  ${colors.green}status${colors.reset}             Show status of Unity projects and connections
  ${colors.green}projects${colors.reset}           List all registered Unity projects
  ${colors.green}help${colors.reset}               Show this help message

${colors.bright}EXAMPLES${colors.reset}
  ${colors.dim}# Start the MCP server${colors.reset}
  unityvision-mcp start

  ${colors.dim}# Auto-configure Windsurf${colors.reset}
  unityvision-mcp setup --client windsurf

  ${colors.dim}# Show all Unity projects${colors.reset}
  unityvision-mcp projects

${colors.bright}MORE INFO${colors.reset}
  https://github.com/nicweberdev/UnityVision
`);
}

// Setup command - configure AI client
async function setupCommand(clientArg?: string): Promise<void> {
  printHeader();
  console.log(`${colors.bright}Setting up UnityVision MCP...${colors.reset}\n`);

  const clients = getAIClients();
  
  if (clients.length === 0) {
    console.log(`${colors.yellow}⚠ No supported AI clients detected.${colors.reset}`);
    console.log(`\nSupported clients: Windsurf, Claude Desktop, Cursor`);
    console.log(`\nManual setup: Add this to your MCP config:\n`);
    printConfigSnippet();
    return;
  }

  let targetClient: AIClientConfig | undefined;

  if (clientArg) {
    const normalized = clientArg.toLowerCase();
    targetClient = clients.find(c => c.name.toLowerCase().includes(normalized));
    if (!targetClient) {
      console.log(`${colors.red}✗ Client "${clientArg}" not found.${colors.reset}`);
      console.log(`Available clients: ${clients.map(c => c.name).join(', ')}`);
      return;
    }
  } else if (clients.length === 1) {
    targetClient = clients[0];
  } else {
    // Prompt user to select
    console.log(`${colors.bright}Detected AI clients:${colors.reset}`);
    clients.forEach((c, i) => {
      console.log(`  ${i + 1}. ${c.name}`);
    });
    
    const rl = readline.createInterface({
      input: process.stdin,
      output: process.stdout,
    });

    const answer = await new Promise<string>(resolve => {
      rl.question(`\nSelect client (1-${clients.length}): `, resolve);
    });
    rl.close();

    const index = parseInt(answer) - 1;
    if (index >= 0 && index < clients.length) {
      targetClient = clients[index];
    } else {
      console.log(`${colors.red}Invalid selection.${colors.reset}`);
      return;
    }
  }

  console.log(`\n${colors.bright}Configuring ${targetClient.name}...${colors.reset}`);

  // Get server path
  const serverPath = getServerPath();
  
  // Read existing config or create new
  let config: Record<string, unknown> = {};
  if (fs.existsSync(targetClient.configPath)) {
    try {
      const content = fs.readFileSync(targetClient.configPath, 'utf-8');
      config = JSON.parse(content);
      
      // Backup existing config
      const backupPath = targetClient.configPath + '.backup';
      fs.writeFileSync(backupPath, content);
      console.log(`${colors.dim}  Backed up existing config to ${backupPath}${colors.reset}`);
    } catch (e) {
      console.log(`${colors.yellow}  Warning: Could not parse existing config, creating new one${colors.reset}`);
    }
  }

  // Ensure mcpServers object exists
  if (!config[targetClient.configKey]) {
    config[targetClient.configKey] = {};
  }

  // Add UnityVision entry
  const mcpServers = config[targetClient.configKey] as Record<string, unknown>;
  mcpServers['unity-vision'] = {
    command: 'node',
    args: [serverPath],
  };

  // Ensure directory exists
  const configDir = path.dirname(targetClient.configPath);
  if (!fs.existsSync(configDir)) {
    fs.mkdirSync(configDir, { recursive: true });
  }

  // Write config
  fs.writeFileSync(targetClient.configPath, JSON.stringify(config, null, 2));

  console.log(`${colors.green}✓ Configuration written to ${targetClient.configPath}${colors.reset}`);
  console.log(`\n${colors.bright}Next steps:${colors.reset}`);
  console.log(`  1. Restart ${targetClient.name}`);
  console.log(`  2. Open a Unity project with UnityVision installed`);
  console.log(`  3. Ask the AI to interact with Unity!`);
}

// Get server.js path
function getServerPath(): string {
  // If running from npm global install, use the installed path
  const scriptPath = process.argv[1];
  const distDir = path.dirname(scriptPath);
  const serverPath = path.join(distDir, 'server.js');
  
  if (fs.existsSync(serverPath)) {
    return serverPath.replace(/\\/g, '/');
  }
  
  // Fallback to relative path from package
  return path.join(__dirname, 'server.js').replace(/\\/g, '/');
}

// Print config snippet
function printConfigSnippet(): void {
  const serverPath = getServerPath();
  console.log(`${colors.cyan}{
  "mcpServers": {
    "unity-vision": {
      "command": "node",
      "args": ["${serverPath}"]
    }
  }
}${colors.reset}`);
}

// Status command
async function statusCommand(): Promise<void> {
  printHeader();
  console.log(`${colors.bright}UnityVision Status${colors.reset}\n`);

  // Check projects
  const projects = readRegistry();
  const staleThreshold = 30000; // 30 seconds
  const now = Date.now();

  const activeProjects = projects.filter(p => {
    const lastSeen = new Date(p.lastSeen).getTime();
    return now - lastSeen < staleThreshold;
  });

  console.log(`${colors.bright}Unity Projects${colors.reset}`);
  if (activeProjects.length === 0) {
    console.log(`  ${colors.dim}No active Unity projects found${colors.reset}`);
    console.log(`  ${colors.dim}Open a Unity project with UnityVision installed${colors.reset}`);
  } else {
    for (const project of activeProjects) {
      const isHealthy = await checkProjectHealth(project.port);
      const status = isHealthy 
        ? `${colors.green}● Connected${colors.reset}` 
        : `${colors.red}● Disconnected${colors.reset}`;
      
      console.log(`  ${status} ${colors.bright}${project.projectName}${colors.reset}`);
      console.log(`    ${colors.dim}Port: ${project.port} | Unity ${project.unityVersion}${colors.reset}`);
      console.log(`    ${colors.dim}${project.projectPath}${colors.reset}`);
    }
  }

  // Check AI clients
  console.log(`\n${colors.bright}AI Clients${colors.reset}`);
  const clients = getAIClients();
  if (clients.length === 0) {
    console.log(`  ${colors.dim}No supported AI clients detected${colors.reset}`);
  } else {
    for (const client of clients) {
      const hasConfig = fs.existsSync(client.configPath);
      let hasUnityVision = false;
      
      if (hasConfig) {
        try {
          const content = fs.readFileSync(client.configPath, 'utf-8');
          const config = JSON.parse(content);
          hasUnityVision = config[client.configKey]?.['unity-vision'] !== undefined;
        } catch {
          // Ignore
        }
      }

      const status = hasUnityVision
        ? `${colors.green}● Configured${colors.reset}`
        : `${colors.yellow}○ Not configured${colors.reset}`;
      
      console.log(`  ${status} ${client.name}`);
    }
  }

  console.log(`\n${colors.dim}Run 'unityvision-mcp setup' to configure an AI client${colors.reset}`);
}

// Projects command
async function projectsCommand(): Promise<void> {
  printHeader();
  console.log(`${colors.bright}Registered Unity Projects${colors.reset}\n`);

  const projects = readRegistry();
  
  if (projects.length === 0) {
    console.log(`${colors.dim}No projects registered.${colors.reset}`);
    console.log(`\nOpen a Unity project with UnityVision installed to register it.`);
    return;
  }

  const staleThreshold = 30000;
  const now = Date.now();

  for (const project of projects) {
    const lastSeen = new Date(project.lastSeen).getTime();
    const isStale = now - lastSeen > staleThreshold;
    const isHealthy = !isStale && await checkProjectHealth(project.port);

    let status: string;
    if (isHealthy) {
      status = `${colors.green}● Active${colors.reset}`;
    } else if (isStale) {
      status = `${colors.dim}○ Stale${colors.reset}`;
    } else {
      status = `${colors.red}● Unreachable${colors.reset}`;
    }

    console.log(`${status} ${colors.bright}${project.projectName}${colors.reset}`);
    console.log(`  ${colors.dim}Path:${colors.reset} ${project.projectPath}`);
    console.log(`  ${colors.dim}Port:${colors.reset} ${project.port}`);
    console.log(`  ${colors.dim}Unity:${colors.reset} ${project.unityVersion}`);
    console.log(`  ${colors.dim}PID:${colors.reset} ${project.pid}`);
    console.log(`  ${colors.dim}Last seen:${colors.reset} ${new Date(project.lastSeen).toLocaleString()}`);
    console.log();
  }
}

// Start command - launch MCP server
function startCommand(): void {
  // Import and run the server
  import('./server.js');
}

// Main entry point
async function main(): Promise<void> {
  const args = process.argv.slice(2);
  const command = args[0]?.toLowerCase();

  switch (command) {
    case 'start':
    case undefined:
      // Default to starting the server (for MCP client compatibility)
      startCommand();
      break;

    case 'setup': {
      const clientIndex = args.indexOf('--client');
      const clientArg = clientIndex >= 0 ? args[clientIndex + 1] : undefined;
      await setupCommand(clientArg);
      break;
    }

    case 'status':
      await statusCommand();
      break;

    case 'projects':
      await projectsCommand();
      break;

    case 'help':
    case '--help':
    case '-h':
      printHelp();
      break;

    default:
      console.log(`${colors.red}Unknown command: ${command}${colors.reset}`);
      console.log(`Run 'unityvision-mcp help' for usage information.`);
      process.exit(1);
  }
}

main().catch(console.error);
