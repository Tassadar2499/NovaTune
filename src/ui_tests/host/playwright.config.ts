import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from '@playwright/test';
import { adminCredentials } from './tests/support/runtime.js';

const currentDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(currentDir, '../../..');
const appHostProject = path.join(
  repoRoot,
  'src',
  'NovaTuneApp',
  'NovaTuneApp.AppHost',
  'NovaTuneApp.AppHost.csproj'
);

export default defineConfig({
  testDir: path.join(currentDir, 'tests'),
  fullyParallel: false,
  workers: 1,
  timeout: 60_000,
  expect: {
    timeout: 10_000,
  },
  reporter: [
    ['list'],
    ['html', { open: 'never', outputFolder: path.join(currentDir, 'playwright-report') }],
  ],
  outputDir: path.join(currentDir, 'test-results'),
  use: {
    headless: !process.env.UI_TESTS_HEADED,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'player',
      testMatch: /player\/.*\.spec\.ts/,
      use: {
        baseURL: 'http://127.0.0.1:25173/',
      },
    },
    {
      name: 'admin',
      testMatch: /admin\/.*\.spec\.ts/,
      use: {
        baseURL: 'http://127.0.0.1:25174/admin/',
      },
    },
  ],
  webServer: {
    command: `dotnet run --project "${appHostProject}" -- --environment=Development`,
    cwd: repoRoot,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_ENVIRONMENT: 'Development',
      AdminSeed__Email: adminCredentials.email,
      AdminSeed__Password: adminCredentials.password,
      AdminSeed__DisplayName: 'E2E Admin',
    },
    url: 'http://127.0.0.1:25174/admin/auth/login',
    reuseExistingServer: !process.env.CI,
    timeout: 600_000,
  },
});
