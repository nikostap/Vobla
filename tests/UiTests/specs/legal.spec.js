import { test, expect } from "@playwright/test";

test("pre-release legal notices are published and reachable from the global footer", async ({ page, request }) => {
  const documents = [
    ["/Privacy", "Уведомление о конфиденциальности"],
    ["/Terms", "Условия использования"],
    ["/Cookies", "Cookie и сессии"],
    ["/Rules", "Правила объявлений"]
  ];

  for (const [path, heading] of documents) {
    const response = await request.get(path);
    expect(response.status()).toBe(200);
    expect(await response.text()).toContain(heading);
  }

  await page.goto("/");
  const footer = page.locator(".site-footer");
  await expect(footer.getByRole("link", { name: "Конфиденциальность" })).toHaveAttribute("href", "/Privacy");
  await expect(footer.getByRole("link", { name: "Условия использования" })).toHaveAttribute("href", "/Terms");
  await footer.getByRole("link", { name: "Cookie и сессии" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Cookie и сессии");
  await expect(page.getByText("нет рекламных или сторонних аналитических cookies", { exact: false })).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/Privacy");
  const horizontalOverflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(horizontalOverflow).toBeLessThanOrEqual(1);
  await expect(page.locator(".site-footer").getByRole("link", { name: "Правила объявлений" })).toBeVisible();
});
