import { test, expect } from "@playwright/test";

test("OTP verification is rate limited per client and returns retry guidance", async ({ request }) => {
  let limitedResponse;
  for (let attempt = 0; attempt < 45; attempt++) {
    const response = await request.post("/Account/SignIn?handler=Verify", {
      form: { Email: "rate-limit@example.test", Code: "000000" }
    });
    if (response.status() === 429) { limitedResponse = response; break; }
    expect(response.status()).toBe(400);
  }
  expect(limitedResponse?.status()).toBe(429);
  expect(limitedResponse?.headers()["retry-after"]).toBe("60");
  const metrics = await (await request.get("/metrics")).text();
  expect(metrics).toContain('marketplace_auth_events_total{action="verify",outcome="rate_limited"}');
});
