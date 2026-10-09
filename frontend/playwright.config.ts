import { defineConfig, devices } from "@playwright/test";

const API_PORT = Number(process.env.API_PORT ?? 5188);
const WEB_PORT = Number(process.env.WEB_PORT ?? 3100);
const IDP_PORT = Number(process.env.IDP_PORT ?? 5190);

/**
 * End-to-end tests drive the real stack: MySQL → .NET API → Next.js (production build) → Chromium, with a fake
 * Google IdP. See e2e/README.md for prerequisites.
 */
export default defineConfig({
  testDir: "./e2e",
  testMatch: /.*\.spec\.ts/,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [["list"]],
  globalSetup: "./e2e/global-setup.ts",
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    trace: "retain-on-failure",
    locale: "fr-FR",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: "node e2e/fake-idp.mjs",
      url: `http://localhost:${IDP_PORT}/health`,
      env: { IDP_PORT: String(IDP_PORT), IDP_REDIRECT_URI: `http://localhost:${WEB_PORT}/api/auth/google/callback` },
      reuseExistingServer: false,
      timeout: 15_000,
    },
    {
      command: "bash e2e/start-api.sh",
      url: `http://localhost:${API_PORT}/health/live`,
      env: { API_PORT: String(API_PORT), WEB_PORT: String(WEB_PORT), IDP_PORT: String(IDP_PORT) },
      reuseExistingServer: false,
      timeout: 240_000,
    },
    {
      // The /api rewrite target is baked in at build time, hence build + start with the same API_URL.
      command: `npx next build && npx next start -p ${WEB_PORT}`,
      url: `http://localhost:${WEB_PORT}/`,
      env: { API_URL: `http://localhost:${API_PORT}`, NEXT_PUBLIC_SITE_URL: `http://localhost:${WEB_PORT}` },
      reuseExistingServer: false,
      timeout: 240_000,
    },
  ],
});
