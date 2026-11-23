import { defineConfig, devices } from '@playwright/test';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// End-to-end tests drive the real Angular client against the real ASP.NET Core API.
// The API runs on SQLite with a brand-new database file for every run, seeded with
// the demo café, so tests always start from the same known data.
const dbFile = join(tmpdir(), `shiftwise-e2e-${Date.now()}.db`);
const isCi = !!process.env['CI'];

export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.e2e.ts',
  // One shared database, so tests run one after another.
  fullyParallel: false,
  workers: 1,
  forbidOnly: isCi,
  retries: isCi ? 1 : 0,
  // Results live outside client/ so writing traces and screenshots never makes the
  // Angular dev server notice a file change and reload the page mid-test.
  outputDir: '../.e2e/test-results',
  reporter: isCi ? [['list'], ['html', { open: 'never', outputFolder: '../.e2e/report' }]] : 'list',
  use: {
    baseURL: 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: 'dotnet run --project ../src/ShiftWise.Api --no-launch-profile',
      url: 'http://localhost:5080/api/auth/me',
      // /api/auth/me answers 401 without a token, which still proves the API is up.
      ignoreHTTPSErrors: true,
      reuseExistingServer: false,
      timeout: 180_000,
      env: {
        ASPNETCORE_URLS: 'http://localhost:5080',
        ASPNETCORE_ENVIRONMENT: 'Development',
        Database__Provider: 'Sqlite',
        ConnectionStrings__Sqlite: `Data Source=${dbFile}`,
        SeedDemoData: 'true',
      },
    },
    {
      command: 'npm start -- --port 4200',
      url: 'http://localhost:4200',
      reuseExistingServer: !isCi,
      timeout: 180_000,
    },
  ],
});
