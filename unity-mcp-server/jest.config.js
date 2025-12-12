// ============================================================================
// UnityVision MCP Server - Jest Configuration
// ============================================================================

/** @type {import('jest').Config} */
const config = {
  // Use ts-jest for TypeScript
  preset: 'ts-jest/presets/default-esm',

  // Test environment
  testEnvironment: 'node',

  // File extensions
  moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json'],

  // Test file patterns
  testMatch: [
    '**/tests/**/*.test.ts',
    '**/tests/**/*.spec.ts',
  ],

  // Transform TypeScript files
  transform: {
    '^.+\\.tsx?$': [
      'ts-jest',
      {
        useESM: true,
        tsconfig: 'tsconfig.json',
      },
    ],
  },

  // Module name mapping for ESM
  moduleNameMapper: {
    '^(\\.{1,2}/.*)\\.js$': '$1',
  },

  // ESM support
  extensionsToTreatAsEsm: ['.ts'],

  // Coverage configuration
  collectCoverageFrom: [
    'src/**/*.ts',
    '!src/**/*.d.ts',
    '!src/cli.ts',
  ],

  // Coverage thresholds (low for now since we use mocks)
  // Increase these when adding integration tests
  coverageThreshold: {
    global: {
      branches: 0,
      functions: 0,
      lines: 0,
      statements: 0,
    },
  },

  // Coverage reporters
  coverageReporters: ['text', 'lcov', 'html'],

  // Test timeout
  testTimeout: 30000,

  // Verbose output
  verbose: true,

  // Clear mocks between tests
  clearMocks: true,

  // Restore mocks after each test
  restoreMocks: true,

  // Setup files
  setupFilesAfterEnv: ['<rootDir>/tests/setup.ts'],

  // Ignore patterns
  testPathIgnorePatterns: [
    '/node_modules/',
    '/dist/',
  ],

  // Transform ignore patterns
  transformIgnorePatterns: [
    'node_modules/(?!(@modelcontextprotocol)/)',
  ],

  // Global setup/teardown for integration tests
  // globalSetup: '<rootDir>/tests/globalSetup.ts',
  // globalTeardown: '<rootDir>/tests/globalTeardown.ts',
};

export default config;
