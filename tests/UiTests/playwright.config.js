import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./specs",
  fullyParallel: false,
  workers: 1,
  timeout: 30_000,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: "http://127.0.0.1:5080",
    browserName: "chromium",
    screenshot: "only-on-failure",
    trace: "retain-on-failure",
    colorScheme: "light"
  },
  expect: {
    toHaveScreenshot: { animations: "disabled", maxDiffPixelRatio: 0.01 }
  },
  snapshotPathTemplate: "{testDir}/snapshots/{testFilePath}/{arg}{ext}",
  webServer: {
    command: "dotnet run --project ../../src/Marketplace.Web --urls http://127.0.0.1:5080",
    url: "http://127.0.0.1:5080/health",
    reuseExistingServer: true,
    timeout: 120_000
  }
});
