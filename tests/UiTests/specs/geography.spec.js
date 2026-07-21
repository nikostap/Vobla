import { test, expect } from "@playwright/test";

test("full address remains available when browser geolocation is denied", async ({ page }) => {
  await page.route("**/maps-api/api/v1/geo/suggest**", async (route) => route.fulfill({
    contentType: "application/json",
    body: JSON.stringify([{ displayName: "Тверская улица, 12, Москва, Россия", latitude: 55.763852, longitude: 37.607301 }])
  }));
  await page.goto("/");
  await expect(page.getByRole("button", { name: /Москва/ })).toBeVisible();
  await page.getByRole("button", { name: /Москва/ }).click();
  await expect(page.getByRole("heading", { name: "Где искать?", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Определить по геолокации", exact: true }).click();
  await expect(page.locator(".geo-permission__status")).toContainText(/Доступ не предоставлен|Заполните адрес вручную/, { timeout: 10_000 });

  await page.getByLabel("Адрес", { exact: true }).fill("Москва, Тверская, 12");
  await expect(page.locator("#search-address-suggestions [role=option]")).toHaveCount(1);
  await page.getByRole("option", { name: "Тверская улица, 12, Москва, Россия", exact: true }).click();
  await page.getByLabel("Радиус", { exact: true }).selectOption("100");
  await page.getByRole("button", { name: "Искать по адресу", exact: true }).click();
  await expect(page).toHaveURL(/latitude=55.763852/);
  await expect(page.getByRole("button", { name: /Тверская улица, 12/ })).toBeVisible();
  await expect(page.locator(".listing-card")).toHaveCount(8);
  await page.reload();
  await expect(page.getByRole("button", { name: /Тверская улица, 12/ })).toBeVisible();
});

test("browser coordinates resolve through adapter and map mode is saved", async ({ page }) => {
  const response = await page.request.get("/?handler=ResolveLocation&latitude=59.93&longitude=30.33");
  expect(response.ok()).toBeTruthy();
  expect((await response.json()).city).toBe("Санкт-Петербург");

  await page.goto("/?mode=map");
  await expect(page.locator(".catalog-layout")).toHaveClass(/mode-map/);
  await expect(page.locator(".marketplace-map-shell")).toBeVisible();
  await expect(page.locator(".maplibregl-canvas")).toBeVisible({ timeout: 15_000 });
  await expect(page.locator(".listing-grid")).toBeHidden();
  await page.goto("/");
  await expect(page.locator(".catalog-layout")).toHaveClass(/mode-map/);
});

test("distant clusters split into individual previews on zoom", async ({ page }) => {
  const bbox = "west=36.5&south=55&east=38.5&north=56.5";
  const distantResponse = await page.request.get(`/maps-api/api/v1/map/clusters?${bbox}&zoom=4`);
  const closeResponse = await page.request.get(`/maps-api/api/v1/map/clusters?${bbox}&zoom=18`);
  expect(distantResponse.ok()).toBeTruthy();
  expect(closeResponse.ok()).toBeTruthy();
  const distant = await distantResponse.json();
  const close = await closeResponse.json();
  expect(close.clusters.length + close.items.length).toBeGreaterThan(distant.clusters.length + distant.items.length);
  await page.goto("/?zoom=18&mode=split");
  await expect(page.locator("body")).not.toContainText(/ExactAddress|точный частный адрес/i);
});
