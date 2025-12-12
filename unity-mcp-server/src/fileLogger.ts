// ============================================================================
// UnityVision MCP Server - File Logger
// Writes detailed debug logs to a file for troubleshooting
// ============================================================================

import * as fs from 'fs';
import * as path from 'path';

const LOG_FILE = path.join(process.cwd(), 'mcp-debug.log');
const MAX_LOG_SIZE = 5 * 1024 * 1024; // 5MB max

/**
 * Rotate log file if it gets too large
 */
function rotateLogIfNeeded(): void {
  try {
    if (fs.existsSync(LOG_FILE)) {
      const stats = fs.statSync(LOG_FILE);
      if (stats.size > MAX_LOG_SIZE) {
        const backupPath = LOG_FILE + '.old';
        if (fs.existsSync(backupPath)) {
          fs.unlinkSync(backupPath);
        }
        fs.renameSync(LOG_FILE, backupPath);
      }
    }
  } catch {
    // Ignore rotation errors
  }
}

/**
 * Write a log entry to the debug log file
 */
export function fileLog(level: 'DEBUG' | 'INFO' | 'WARN' | 'ERROR', component: string, message: string, data?: unknown): void {
  try {
    rotateLogIfNeeded();
    
    const timestamp = new Date().toISOString();
    let logLine = `[${timestamp}] [${level}] [${component}] ${message}`;
    
    if (data !== undefined) {
      try {
        const dataStr = JSON.stringify(data, null, 0);
        // Truncate very large data
        if (dataStr.length > 2000) {
          logLine += ` | DATA: ${dataStr.substring(0, 2000)}... (truncated, total ${dataStr.length} chars)`;
        } else {
          logLine += ` | DATA: ${dataStr}`;
        }
      } catch {
        logLine += ` | DATA: [unserializable]`;
      }
    }
    
    logLine += '\n';
    
    fs.appendFileSync(LOG_FILE, logLine, 'utf8');
  } catch {
    // Silently fail if we can't write logs
  }
}

/**
 * Log request start
 */
export function logRequestStart(requestId: string, method: string, params: unknown): void {
  fileLog('INFO', 'REQUEST', `START ${requestId} - ${method}`, { params });
}

/**
 * Log request phase
 */
export function logRequestPhase(requestId: string, phase: string, details?: unknown): void {
  fileLog('DEBUG', 'REQUEST', `PHASE ${requestId} - ${phase}`, details);
}

/**
 * Log request end
 */
export function logRequestEnd(requestId: string, method: string, durationMs: number, success: boolean, error?: string): void {
  const level = success ? 'INFO' : 'ERROR';
  fileLog(level, 'REQUEST', `END ${requestId} - ${method} - ${durationMs}ms - ${success ? 'SUCCESS' : 'FAILED'}`, { error });
}

/**
 * Log connection events
 */
export function logConnection(event: string, details?: unknown): void {
  fileLog('INFO', 'CONNECTION', event, details);
}

/**
 * Log errors
 */
export function logError(component: string, message: string, error?: unknown): void {
  fileLog('ERROR', component, message, { error: error instanceof Error ? error.message : error });
}

/**
 * Get log file path
 */
export function getLogFilePath(): string {
  return LOG_FILE;
}

/**
 * Clear the log file
 */
export function clearLog(): void {
  try {
    fs.writeFileSync(LOG_FILE, `=== Log cleared at ${new Date().toISOString()} ===\n`, 'utf8');
  } catch {
    // Ignore
  }
}

// Initialize log on module load
fileLog('INFO', 'INIT', `MCP Debug Logger initialized. Log file: ${LOG_FILE}`);
