# UnityVision MCP Server - Test Suite

## Overview

This test suite provides comprehensive testing for the UnityVision MCP Server, including:

- **Unit Tests**: Test individual tools and handlers in isolation
- **Integration Tests**: Test end-to-end functionality with a running Unity instance
- **Mock Tests**: Test with mocked Unity bridge responses

## Test Structure

```
tests/
├── setup.ts                    # Common test utilities and mocks
├── README.md                   # This file
├── tools/                      # Unit tests for each tool
│   ├── editorTools.test.ts     # unity_editor tool tests
│   ├── scriptTools.test.ts     # unity_script tool tests
│   ├── packageTools.test.ts    # unity_package tool tests
│   ├── sceneTools.test.ts      # unity_scene tool tests
│   └── gameObjectTools.test.ts # unity_gameobject tool tests
└── integration/                # Integration tests (require Unity)
    ├── bridge.test.ts          # Bridge connection tests
    └── resources.test.ts       # MCP Resources tests
```

## Running Tests

### Prerequisites

1. Install dependencies:
   ```bash
   cd unity-mcp-server
   npm install
   ```

2. Build the project:
   ```bash
   npm run build
   ```

### Unit Tests (No Unity Required)

Run all unit tests:
```bash
npm test
```

Run tests in watch mode (for development):
```bash
npm run test:watch
```

Run tests with coverage report:
```bash
npm run test:coverage
```

### Integration Tests (Unity Required)

Integration tests require a running Unity instance with the UnityVision bridge package installed.

1. Open Unity with a project that has UnityVision installed
2. Ensure the bridge is running (check Window > UnityVision > Bridge Status)
3. Run integration tests:

```bash
npm run test:integration
```

Or on Windows:
```powershell
$env:UNITY_INTEGRATION_TESTS=1; npm test -- --testPathPattern=integration
```

## Test Coverage

Current test coverage targets:
- **Branches**: 50%
- **Functions**: 50%
- **Lines**: 50%
- **Statements**: 50%

View coverage report after running:
```bash
npm run test:coverage
```

Coverage report will be generated in `coverage/` directory.

## Writing New Tests

### Unit Test Template

```typescript
import { describe, it, expect, beforeEach, jest } from '@jest/globals';
import { mockBridgeClient, setupMockResponse, resetMocks } from '../setup.js';

jest.mock('../../src/unityBridgeClient.js', () => ({
  getBridgeClient: () => mockBridgeClient,
}));

describe('my_tool Tool', () => {
  beforeEach(() => {
    resetMocks();
  });

  describe('action_name action', () => {
    it('should do something', async () => {
      setupMockResponse('method_name', { success: true });
      
      const result = await mockBridgeClient.call('method_name', {});
      
      expect(result.success).toBe(true);
    });
  });
});
```

### Integration Test Template

```typescript
import { describe, it, expect } from '@jest/globals';

const SKIP_INTEGRATION = !process.env.UNITY_INTEGRATION_TESTS;

describe('Integration Test', () => {
  const testFn = SKIP_INTEGRATION ? it.skip : it;

  testFn('should work with real Unity', async () => {
    // Test with real Unity connection
  });
});
```

## Test Tools Coverage

| Tool | Unit Tests | Integration Tests |
|------|:----------:|:-----------------:|
| `unity_editor` | ✅ | ✅ |
| `unity_console` | ⏳ | ⏳ |
| `unity_scene` | ✅ | ✅ |
| `unity_gameobject` | ✅ | ✅ |
| `unity_component` | ⏳ | ⏳ |
| `unity_selection` | ⏳ | ⏳ |
| `unity_asset` | ⏳ | ⏳ |
| `unity_material` | ⏳ | ⏳ |
| `unity_prefab` | ⏳ | ⏳ |
| `unity_query` | ⏳ | ⏳ |
| `unity_dependency` | ⏳ | ⏳ |
| `unity_animation` | ⏳ | ⏳ |
| `unity_audio` | ⏳ | ⏳ |
| `unity_profiler` | ⏳ | ⏳ |
| `unity_screenshot` | ⏳ | ⏳ |
| `unity_xr` | ⏳ | ⏳ |
| `unity_shadergraph` | ⏳ | ⏳ |
| `unity_ui` | ⏳ | ⏳ |
| `unity_menu` | ⏳ | ⏳ |
| `unity_code` | ⏳ | ⏳ |
| `unity_test` | ⏳ | ⏳ |
| `unity_build` | ⏳ | ⏳ |
| `unity_project` | ⏳ | ⏳ |
| `unity_batch` | ⏳ | ⏳ |
| `unity_script` | ✅ | ✅ |
| `unity_package` | ✅ | ✅ |

Legend: ✅ = Implemented, ⏳ = Pending

## MCP Resources Coverage

| Resource | Unit Tests | Integration Tests |
|----------|:----------:|:-----------------:|
| `unity://hierarchy` | ⏳ | ✅ |
| `unity://selection` | ⏳ | ✅ |
| `unity://logs` | ⏳ | ✅ |
| `unity://project-info` | ⏳ | ✅ |
| `unity://editor-state` | ⏳ | ✅ |

## Troubleshooting

### Tests fail with "Cannot find module '@jest/globals'"

Run `npm install` to install Jest dependencies.

### Integration tests skip with "Skipping integration tests"

Set the environment variable:
```bash
export UNITY_INTEGRATION_TESTS=1
```

### Connection refused errors in integration tests

1. Ensure Unity is open
2. Check that the bridge is running (Window > UnityVision > Bridge Status)
3. Verify the port (default: 7891 for WebSocket)

### Tests timeout

Increase the timeout in `jest.config.js`:
```javascript
testTimeout: 60000, // 60 seconds
```

## CI/CD Integration

For CI/CD pipelines, unit tests can run without Unity:

```yaml
# GitHub Actions example
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '18'
      - run: cd unity-mcp-server && npm install
      - run: cd unity-mcp-server && npm test
```

Integration tests require a Unity license and are typically run manually or in a dedicated environment.
