const { defineConfig } = require('@playwright/test');
const path = require('path');
require('dotenv').config({ path: path.join(__dirname, '.env') });

const baseURL = process.env.BASE_URL || 'http://127.0.0.1:5187';

module.exports = defineConfig({
  testDir: './tests',
  timeout: 45_000,
  expect: {
    timeout: 10_000,
  },
  fullyParallel: false,
  workers: 1, // Sequential execution for deterministic state flow testing
  reporter: [
    ['list'],
    ['html', { outputFolder: process.env.HTML_REPORT_DIR || 'playwright-report', open: 'never' }]
  ],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    extraHTTPHeaders: {
      'Accept': 'application/json',
      'X-Client-Platform': 'Playwright-API-Tests'
    }
  }
});