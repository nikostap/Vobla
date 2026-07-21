import { test, expect } from "@playwright/test";

test("theme is persisted and component states are accessible", async ({ page }) => {
  await page.goto("/Components");

  const themeToggle = page.getByRole("button", { name: "Включить тёмную тему" });
  await themeToggle.click();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
  await page.reload();
  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

  await expect(page.locator(".ui-button:disabled")).toBeDisabled();
  await expect(page.locator(".ui-chip:disabled")).toBeDisabled();
  await expect(page.getByLabel("Цена")).toHaveAttribute("aria-invalid", "true");

  await page.getByRole("button", { name: "Открыть диалог" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toBeHidden();
});

test("empty states keep descriptions with headings and actions separated", async ({ page }) => {
  await page.goto("/Components");
  const panel = page.locator(".state-panel", { has: page.getByRole("button", { name: "Повторить", exact: true }) });
  const spacing = await panel.evaluate((element) => {
    const heading = element.querySelector("h3");
    const description = element.querySelector("p");
    const action = element.querySelector(".ui-button");
    const headingBox = heading.getBoundingClientRect();
    const descriptionBox = description.getBoundingClientRect();
    const actionBox = action.getBoundingClientRect();
    return {
      headingToDescription: descriptionBox.top - headingBox.bottom,
      descriptionToAction: actionBox.top - descriptionBox.bottom
    };
  });
  expect(spacing.headingToDescription).toBeLessThanOrEqual(8);
  expect(spacing.descriptionToAction).toBeGreaterThanOrEqual(18);
});

test("links and filter actions use the marketplace palette in both themes", async ({ page }) => {
  const forbiddenBrowserColors = new Set(["rgb(0, 0, 238)", "rgb(85, 26, 139)", "rgb(0, 102, 204)"]);

  for (const theme of ["light", "dark"]) {
    await page.goto("/?condition=New");
    if (theme === "dark") await page.getByRole("button", { name: "Включить тёмную тему" }).click();
    await expect(page.locator("html")).toHaveAttribute("data-theme", theme);
    await page.locator('[data-dialog-open="filter-dialog"]').click();
    await expect(page.locator("#filter-dialog")).toBeVisible();

    const colors = await page.locator("a").evaluateAll((links) => links
      .filter((link) => link.getClientRects().length > 0)
      .map((link) => getComputedStyle(link).color));
    expect(colors.some((color) => forbiddenBrowserColors.has(color))).toBe(false);

    const resetColor = await page.getByRole("link", { name: "Сбросить всё", exact: true }).evaluate((link) => getComputedStyle(link).color);
    const resetChannels = resetColor.match(/\d+/g).map(Number).slice(0, 3);
    if (theme === "dark") expect(Math.min(...resetChannels)).toBeGreaterThan(180);
    else expect(Math.max(...resetChannels)).toBeLessThan(100);
    await page.getByRole("button", { name: "Закрыть", exact: true }).click();
  }
});

test("unified list pagination changes pages without navigation", async ({ page }) => {
  await page.goto("/Components");
  await page.evaluate(() => {
    const list = document.createElement("section");
    list.dataset.pagedList = "";
    list.dataset.pageSize = "10";
    for (let index = 1; index <= 23; index += 1) {
      const item = document.createElement("article");
      item.dataset.pageItem = "";
      item.textContent = `Элемент ${index}`;
      list.append(item);
    }
    document.querySelector("main").append(list);
    window.marketplacePagination.initialize(list);
  });
  await expect(page.locator("[data-page-item]:visible")).toHaveCount(10);
  const originalUrl = page.url();
  await page.getByRole("button", { name: "Вперёд →", exact: true }).click();
  await expect(page.locator("[data-page-item]:visible")).toHaveCount(10);
  await expect(page.getByText("11–20 из 23", { exact: true })).toBeVisible();
  expect(page.url()).toBe(originalUrl);
  await page.getByRole("button", { name: "3", exact: true }).click();
  await expect(page.locator("[data-page-item]:visible")).toHaveCount(3);
});

test("catalog header becomes compact after scroll", async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 700 });
  await page.goto("/");
  await page.evaluate(() => window.scrollTo(0, 300));
  await expect(page.locator(".site-header")).toHaveAttribute("data-compact", "");
});

test("catalog header stays stable at the bottom", async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 700 });
  await page.goto("/");

  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await expect(page.locator(".site-header")).toHaveAttribute("data-compact", "");
  await page.waitForTimeout(250);

  const before = await page.evaluate(() => ({
    height: document.querySelector(".site-header").getBoundingClientRect().height,
    scrollY: window.scrollY
  }));

  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await page.waitForTimeout(250);

  const after = await page.evaluate(() => ({
    height: document.querySelector(".site-header").getBoundingClientRect().height,
    scrollY: window.scrollY
  }));

  expect(after).toEqual(before);
});

test("catalog visual baseline", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 950 });
  await page.goto("/");
  await expect(page.locator(".listing-card img").first()).toBeVisible();
  await expect(page).toHaveScreenshot("catalog-light-1440.png", { fullPage: true });

  await page.getByRole("button", { name: "Включить тёмную тему" }).click();
  await expect(page).toHaveScreenshot("catalog-dark-1440.png", { fullPage: true });
});

test("component gallery visual baselines", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 950 });
  await page.goto("/Components");
  await expect(page.locator(".mini-card img")).toBeVisible();
  await expect(page).toHaveScreenshot("components-light-1440.png", { fullPage: true });

  await page.getByRole("button", { name: "Включить тёмную тему" }).click();
  await expect(page).toHaveScreenshot("components-dark-1440.png", { fullPage: true });
});
