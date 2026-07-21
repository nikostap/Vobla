import { test, expect } from "@playwright/test";

test("security headers, readiness and keyboard bypass are active", async ({ page, request }) => {
  const cspViolations = [];
  page.on("console", message => {
    if (message.text().toLowerCase().includes("content security policy")) cspViolations.push(message.text());
  });
  const correlationId = "6fcd3cf8-2385-42e6-8648-9e3b399d7f28";
  const response = await request.get("/", { headers: { "X-Correlation-ID": correlationId } });
  expect(response.status()).toBe(200);
  expect(response.headers()["x-content-type-options"]).toBe("nosniff");
  expect(response.headers()["x-frame-options"]).toBe("DENY");
  expect(response.headers()["referrer-policy"]).toBe("strict-origin-when-cross-origin");
  expect(response.headers()["cross-origin-opener-policy"]).toBe("same-origin");
  expect(response.headers()["cross-origin-resource-policy"]).toBe("same-origin");
  expect(response.headers()["content-security-policy"]).toContain("frame-ancestors 'none'");
  expect(response.headers()["content-security-policy"]).toContain("script-src 'self';");
  expect(response.headers()["content-security-policy"]).not.toContain("script-src 'self' 'unsafe-inline'");
  expect(response.headers()["content-security-policy"]).toContain("style-src 'self';");
  expect(response.headers()["content-security-policy"]).not.toContain("style-src 'self' 'unsafe-inline'");
  expect(response.headers()["x-correlation-id"]).toBe(correlationId);
  expect(response.headers()["cache-control"] ?? "").not.toContain("no-store");
  const signIn = await request.get("/Account/SignIn");
  expect(signIn.headers()["cache-control"]).toContain("no-store");
  const live = await request.get("/health/live");
  expect(live.status()).toBe(200);
  const ready = await request.get("/health/ready");
  expect(ready.status()).toBe(200);
  await page.goto("/");
  await page.keyboard.press("Tab");
  await expect(page.locator(".skip-link")).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("#main-content")).toBeFocused();
  expect(cspViolations).toEqual([]);
});

test("error responses are private, localized and expose only the correlation reference", async ({ request }) => {
  const correlationId = "8a75f864-fb96-4cf5-bdd7-a6ff126a172d";
  const response = await request.get("/Error", { headers: { "X-Correlation-ID": correlationId } });
  expect(response.status()).toBe(500);
  expect(response.headers()["cache-control"]).toContain("no-store");
  expect(response.headers()["x-correlation-id"]).toBe(correlationId);
  const body = await response.text();
  expect(body).toContain("Не удалось выполнить запрос");
  expect(body).toContain(correlationId);
  expect(body).not.toContain("Development Mode");
  expect(body).not.toContain("StackTrace");
});

test("runtime metrics expose bounded HTTP and worker telemetry", async ({ request }) => {
  await request.get("/health/ready");
  const response = await request.get("/metrics");
  expect(response.status()).toBe(200);
  expect(response.headers()["content-type"]).toContain("text/plain");
  const metrics = await response.text();
  expect(metrics).toContain("marketplace_http_requests_total");
  expect(metrics).toContain("marketplace_http_active_requests");
  expect(metrics).toContain("marketplace_http_responses_total{status_class=\"2xx\"}");
  expect(metrics).toContain("marketplace_background_jobs_total");
  expect(metrics).not.toContain("path=");
});

test("public HTML is compressed while authentication pages remain uncompressed", async ({ request }) => {
  const publicResponse = await request.get("/", { headers: { "Accept-Encoding": "br, gzip" } });
  expect(publicResponse.headers()["content-encoding"]).toBe("br");
  expect(publicResponse.headers()["vary"]).toContain("Accept-Encoding");
  const authenticationResponse = await request.get("/Account/SignIn", { headers: { "Accept-Encoding": "br, gzip" } });
  expect(authenticationResponse.headers()["content-encoding"]).toBeUndefined();
});

test("untrusted forwarded headers cannot spoof client IP or HTTPS", async ({ request }) => {
  const response = await request.get("/health/probes/request-context", {
    headers: { "X-Forwarded-For": "203.0.113.42", "X-Forwarded-Proto": "https" }
  });
  expect(response.status()).toBe(200);
  const context = await response.json();
  expect(context.remoteIp).not.toBe("203.0.113.42");
  expect(context.scheme).toBe("http");
});

test("request body limits reject oversized payloads before form parsing", async ({ request }) => {
  const oversized = await request.post("/Account/SignIn?handler=Verify", {
    headers: { "Content-Type": "application/octet-stream" },
    data: Buffer.alloc(2 * 1024 * 1024)
  });
  expect(oversized.status()).toBe(413);
  const chat = await (await request.get("/health/probes/request-limit?path=/Messages/Chat")).json();
  const listing = await (await request.get("/health/probes/request-limit?path=/Listings/Create")).json();
  expect(chat.bytes).toBe(65 * 1024 * 1024);
  expect(listing.bytes).toBe(105 * 1024 * 1024);
});

test("stale upload temp cleanup preserves fresh and completed files", async ({ request }) => {
  const response = await request.get("/health/probes/upload-temp-cleanup");
  expect(response.status()).toBe(200);
  expect(await response.json()).toEqual({ removed: 1, staleExists: false, freshExists: true, completedExists: true });
});

test("catalog has a basic accessible structure and handles a small read load", async ({ page, request }) => {
  await page.goto("/");
  await expect(page.locator("html")).toHaveAttribute("lang", "ru");
  await expect(page.locator("main")).toHaveCount(1);
  expect(await page.locator("img:not([alt])").count()).toBe(0);
  const unlabeled = await page.locator("input:not([type=hidden]), select, textarea").evaluateAll(elements => elements.filter(el => !el.labels?.length && !el.getAttribute("aria-label") && !el.getAttribute("aria-labelledby")).length);
  expect(unlabeled).toBe(0);
  const started = Date.now();
  const responses = await Promise.all(Array.from({ length: 25 }, () => request.get("/health/ready")));
  expect(responses.every(item => item.status() === 200)).toBeTruthy();
  expect(Date.now() - started).toBeLessThan(5000);
});

test("moderation rules sustain a synthetic queue load without persistence", async ({ request }) => {
  const response = await request.get("/health/probes/moderation");
  expect(response.status()).toBe(200);
  const result = await response.json();
  expect(result.evaluated).toBe(10_000);
  expect(result.critical).toBe(1_000);
  expect(result.elapsedMs).toBeLessThan(2000);
});
