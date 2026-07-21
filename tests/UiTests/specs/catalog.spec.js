import { test, expect } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

const widths = [390, 768, 1366, 1920];
const screenshotDir = path.resolve("test-results", "screenshots");

for (const width of widths) {
  test(`catalog shell at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: width <= 768 ? 844 : 1000 });
    await page.goto("/");

    await expect(page).toHaveTitle(/Объявления рядом/);
    await expect(page.getByRole("heading", { name: "Результаты поиска" })).toBeVisible();
    await expect(page.locator(".listing-card")).toHaveCount(8);
    await expect(page.locator(".listing-card img").first()).toBeVisible();

    const bodyOverflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
    expect(bodyOverflow).toBe(false);

    fs.mkdirSync(screenshotDir, { recursive: true });
    await page.screenshot({ path: path.join(screenshotDir, `catalog-${width}.png`), fullPage: true });
  });
}

test("card and real-map marker stay linked", async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 900 });
  await page.goto("/");
  const marker = page.locator(".map-item-marker[data-listing-id]").first();
  await expect(marker).toBeVisible({ timeout: 15_000 });
  const listingId = await marker.getAttribute("data-listing-id");
  const card = page.locator(`.listing-card[data-listing-id="${listingId}"]`);
  const markerCenter = () => marker.evaluate(element => {
    const bounds = element.getBoundingClientRect();
    return { x: bounds.x + bounds.width / 2, y: bounds.y + bounds.height / 2 };
  });
  const centerBeforeHover = await markerCenter();
  const markerMotion = await marker.evaluate(element => ({
    label: element.textContent.trim(),
    transitionProperties: getComputedStyle(element).transitionProperty.split(",").map(value => value.trim())
  }));
  expect(markerMotion.label).not.toBe("");
  expect(markerMotion.transitionProperties).not.toContain("transform");

  await card.hover();
  await expect(marker).toHaveAttribute("data-card-highlighted", "true", { timeout: 15_000 });
  await expect.poll(async () => {
    const centerAfterHover = await markerCenter();
    return Math.max(Math.abs(centerAfterHover.x - centerBeforeHover.x), Math.abs(centerAfterHover.y - centerBeforeHover.y));
  }).toBeLessThan(1);
  await marker.dispatchEvent("mouseenter");
  await expect(card).toHaveAttribute("data-map-highlighted", "true");
});

test("mobile map remains interactive", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/");

  await page.getByRole("button", { name: "Карта" }).click();
  await expect(page.locator(".marketplace-map-shell")).toBeVisible();
  await expect(page.locator(".maplibregl-canvas")).toBeVisible({ timeout: 15_000 });
  await page.getByRole("button", { name: "Показать списком" }).click();
  await expect(page.locator(".listing-grid")).toBeVisible();
});

test("desktop map loads tiles, toggles viewport search and returns to the list", async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 900 });
  await page.goto("/?mode=map");

  const mapShell = page.locator(".marketplace-map-shell");
  await expect(mapShell).toHaveClass(/map-ready/, { timeout: 15_000 });
  await expect(page.locator(".maplibregl-canvas")).toBeVisible();
  await expect(page.locator(".map-cluster-marker, .map-item-marker").first()).toBeVisible({ timeout: 15_000 });
  await expect(page.locator(".map-city")).toBeHidden();

  const searchMoving = page.getByRole("button", { name: "Искать при перемещении карты" });
  await expect(searchMoving).toHaveAttribute("aria-pressed", "true");
  await searchMoving.click();
  await expect(searchMoving).toHaveAttribute("aria-pressed", "false");
  expect(await page.evaluate(() => localStorage.getItem("marketplace-map-search-moving"))).toBe("false");

  await page.getByRole("button", { name: "Показать списком" }).click();
  await expect(page.locator(".catalog-layout")).toHaveClass(/mode-list/);
  await expect(page.locator(".listing-grid")).toBeVisible();
  await expect(mapShell).toBeHidden();
  await expect(page).toHaveURL(/mode=list/);
});

test("map mode renders a sized canvas at the in-app browser width", async ({ page }) => {
  const consoleErrors = [];
  page.on("console", message => { if (message.type() === "error") consoleErrors.push(message.text()); });
  await page.setViewportSize({ width: 858, height: 912 });
  await page.goto("/?mode=map");

  const mapShell = page.locator(".marketplace-map-shell");
  await expect(mapShell).toBeVisible();
  await expect(mapShell).toHaveClass(/map-ready/, { timeout: 15_000 });
  const canvasSize = await page.locator("[data-map-canvas]").evaluate(element => ({ width: element.getBoundingClientRect().width, height: element.getBoundingClientRect().height }));
  expect(canvasSize.width).toBeGreaterThan(500);
  expect(canvasSize.height).toBeGreaterThan(500);
  await expect(page.locator(".map-cluster-marker, .map-item-marker").first()).toBeVisible({ timeout: 15_000 });
  expect(consoleErrors.filter(message => /Invalid sprite URL|Failed to fetch|PMTiles/i.test(message))).toEqual([]);
});

test("map canvas follows viewport resize without leaving a blank strip", async ({ page }) => {
  await page.setViewportSize({ width: 1100, height: 900 });
  await page.goto("/?mode=map");
  await expect(page.locator(".marketplace-map-shell")).toHaveClass(/map-ready/, { timeout: 15_000 });

  const mapMetrics = () => page.locator("[data-map-canvas]").evaluate(element => {
    const canvas = element.querySelector("canvas");
    const host = element.getBoundingClientRect();
    const rendered = canvas?.getBoundingClientRect();
    return {
      hostWidth: host.width,
      hostHeight: host.height,
      canvasWidth: rendered?.width ?? 0,
      canvasHeight: rendered?.height ?? 0,
      backingWidth: canvas?.width ?? 0,
      backingHeight: canvas?.height ?? 0,
      pixelRatio: window.devicePixelRatio
    };
  });
  const expectCanvasToFillHost = async () => {
    await expect.poll(async () => {
      const metrics = await mapMetrics();
      return {
        widthGap: Math.abs(metrics.hostWidth - metrics.canvasWidth),
        heightGap: Math.abs(metrics.hostHeight - metrics.canvasHeight),
        backingWidthGap: Math.abs(metrics.backingWidth - Math.round(metrics.canvasWidth * metrics.pixelRatio)),
        backingHeightGap: Math.abs(metrics.backingHeight - Math.round(metrics.canvasHeight * metrics.pixelRatio))
      };
    }).toEqual({ widthGap: 0, heightGap: 0, backingWidthGap: 0, backingHeightGap: 0 });
  };

  await expectCanvasToFillHost();
  await page.setViewportSize({ width: 760, height: 900 });
  await expectCanvasToFillHost();
  await page.setViewportSize({ width: 1100, height: 900 });
  await expectCanvasToFillHost();
});

test("large catalog results load in server-sized portions", async ({ page }) => {
  await page.goto("/?condition=Used");
  const initialCount = await page.locator(".listing-card").count();
  expect(initialCount).toBeLessThanOrEqual(12);
  const loader = page.locator("[data-feed-loader]");
  const total = Number(await loader.getAttribute("data-total"));
  if (total > initialCount) {
    await loader.scrollIntoViewIfNeeded();
    await expect.poll(() => page.locator(".listing-card").count()).toBeGreaterThan(initialCount);
    const ids = await page.locator(".listing-card").evaluateAll((cards) => cards.map((card) => card.dataset.listingId));
    expect(new Set(ids).size).toBe(ids.length);
    await expect(page).toHaveURL(/condition=Used/);
  }
});

test("homepage navigation opens real product flows and preserves sign-in return", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("link", { name: "Компоненты" })).toHaveCount(0);

  const categoryLinks = page.locator(".category-strip a.category-chip");
  await expect(categoryLinks).toHaveCount(16);
  const moreButton = page.locator("[data-category-more]");
  await expect(moreButton).toHaveAttribute("aria-expanded", "false");
  await moreButton.click();
  await expect(moreButton).toHaveAttribute("aria-expanded", "true");
  await expect(moreButton).toContainText("Свернуть");
  await expect(page).toHaveURL("/");
  await expect(page.locator(".category-chip--extra").first()).toBeVisible();
  await categoryLinks.nth(1).click();
  await expect(page).toHaveURL(/\/?category=transport/);
  await expect(page.getByRole("heading", { name: "Объявления: Транспорт" })).toBeVisible();
  await expect(page.locator('.category-strip .category-chip[href="/?category=transport"]')).toHaveClass(/active/);
  await expect(page.locator(".subcategory-strip a")).toHaveCount(3);
  await expect(page.locator('.subcategory-strip a[href="/?category=transport"]')).toHaveClass(/active/);
  await page.getByRole("link", { name: "Легковые автомобили", exact: true }).click();
  await expect(page).toHaveURL(/category=legkovye-avtomobili/);
  await expect(page.locator('.category-strip .category-chip[href="/?category=transport"]')).toHaveClass(/active/);
  await expect(page.locator('.subcategory-strip a[href="/?category=legkovye-avtomobili"]')).toHaveClass(/active/);

  await page.goto("/");
  await page.getByRole("link", { name: "Новые", exact: true }).click();
  await expect(page).toHaveURL(/condition=New/);
  await expect(page.locator(".listing-card")).toHaveCount(2);
  await expect(page.getByRole("heading", { name: "iPhone 13, 128 ГБ", exact: true })).toBeVisible();

  await page.goto("/");
  await page.getByRole("link", { name: "Бесплатно", exact: true }).click();
  await expect(page).toHaveURL(/dealType=Free/);
  await expect(page.locator(".listing-card")).toHaveCount(1);
  await expect(page.getByRole("heading", { name: "Стол обеденный", exact: true })).toBeVisible();

  await page.goto("/");
  await page.getByRole("link", { name: "Подать объявление" }).click();
  await expect(page).toHaveURL(/\/Account\/SignIn\?ReturnUrl=%2FListings%2FCreate/i);
  const email = `homepage-return-${Date.now()}@example.test`;
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код" }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByLabel("Одноразовый код").fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться" }).click();
  await expect(page).toHaveURL(/\/Listings\/Create/);
  await expect(page.getByRole("heading", { name: "Подайте объявление" })).toBeVisible();
});

test("sorting and the filter dialog update the catalog query", async ({ page }) => {
  await page.goto("/?category=legkovye-avtomobili&hasPhoto=true");

  await page.getByRole("combobox", { name: "Сортировка" }).selectOption("price-desc");
  await expect.poll(() => new URL(page.url()).searchParams.get("sort")).toBe("price-desc");
  expect(new URL(page.url()).searchParams.get("category")).toBe("legkovye-avtomobili");
  expect(new URL(page.url()).searchParams.get("hasPhoto")).toBe("true");

  await page.locator('[data-dialog-open="filter-dialog"]').click();
  const dialog = page.locator("#filter-dialog");
  await expect(dialog).toBeVisible();
  await dialog.getByLabel("Цена от").fill("1000000");
  await dialog.getByLabel("Состояние").selectOption("Used");
  await dialog.locator('button[type="submit"]').click();

  await expect.poll(() => new URL(page.url()).searchParams.get("minPrice")).toBe("1000000");
  expect(new URL(page.url()).searchParams.get("condition")).toBe("Used");
  expect(new URL(page.url()).searchParams.get("category")).toBe("legkovye-avtomobili");
  expect(new URL(page.url()).searchParams.get("sort")).toBe("price-desc");
  await expect(page.locator(".listing-card")).toHaveCount(1);
});

test("newest quick filter can be switched off despite the saved sort", async ({ page }) => {
  await page.goto("/?sort=recommended&mode=list");
  const newest = page.getByRole("link", { name: "Сначала свежие", exact: true });
  await newest.click();
  await expect.poll(() => new URL(page.url()).searchParams.get("sort")).toBe("newest");
  await expect(newest).toHaveClass(/active/);

  await newest.click();
  await expect.poll(() => new URL(page.url()).searchParams.get("sort")).toBe("recommended");
  await expect(newest).not.toHaveClass(/active/);
  await expect(page.getByRole("combobox", { name: "Сортировка" })).toHaveValue("recommended");
});

test("free listings are treated as zero when sorting by price", async ({ page }) => {
  await page.goto("/?sort=price-asc&mode=list");
  const ascendingPrices = await page.locator(".listing-price").allTextContents();
  expect(ascendingPrices[0].trim()).toBe("Бесплатно");

  await page.goto("/?sort=price-desc&mode=list");
  const descendingPrices = await page.locator(".listing-price").allTextContents();
  expect(descendingPrices[descendingPrices.length - 1].trim()).toBe("Бесплатно");
});

test("featured cards open real listing details", async ({ page }) => {
  await page.goto("/");
  const cardLink = page.getByRole("link", { name: "BMW 5 серия, 2018", exact: true });
  await expect(cardLink).toHaveAttribute("href", /\/Listings\/Details\?id=10000000-0000-0000-0000-000000000002/i);
  await cardLink.click();
  await expect(page).toHaveURL(/\/Listings\/Details\?id=10000000-0000-0000-0000-000000000002/i);
  await expect(page.getByRole("heading", { name: "BMW 5 серия, 2018" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Войти, чтобы написать" })).toBeVisible();
  const breadcrumbs = page.getByRole("navigation", { name: "Хлебные крошки" });
  await expect(breadcrumbs.getByRole("link", { name: "Все объявления", exact: true })).toHaveAttribute("href", "/");
  const categoryLink = breadcrumbs.getByRole("link").nth(1);
  const categoryName = (await categoryLink.textContent()).trim();
  await expect(categoryLink).toHaveAttribute("href", /\?category=[^&]+/);
  await categoryLink.click();
  await expect(page.getByRole("heading", { name: `Объявления: ${categoryName}`, exact: true })).toBeVisible();
  expect(new URL(page.url()).searchParams.get("category")).toBeTruthy();
});

test("the whole card opens details and BMW photos scrub under the pointer", async ({ page }) => {
  await page.setViewportSize({ width: 1366, height: 900 });
  await page.goto("/");
  const bmwId = "10000000-0000-0000-0000-000000000002";
  const card = page.locator(`.listing-card[data-listing-id="${bmwId}"]`);
  const media = card.locator("[data-gallery]");
  const images = card.locator("[data-gallery-image]");
  const dots = card.locator("[data-gallery-dot]");

  await expect(images).toHaveCount(3);
  await expect(dots).toHaveCount(3);
  const bounds = await media.boundingBox();
  expect(bounds).not.toBeNull();
  await page.mouse.move(bounds.x + bounds.width * 0.52, bounds.y + bounds.height / 2);
  await expect(images.nth(1)).toHaveClass(/is-active/);
  await expect(dots.nth(1)).toHaveClass(/is-active/);
  await page.mouse.move(bounds.x + bounds.width * 0.9, bounds.y + bounds.height / 2);
  await expect(images.nth(2)).toHaveClass(/is-active/);
  await expect(dots.nth(2)).toHaveClass(/is-active/);
  await page.mouse.move(bounds.x - 10, bounds.y + bounds.height / 2);
  await expect(images.nth(0)).toHaveClass(/is-active/);

  await card.scrollIntoViewIfNeeded();
  const cardBounds = await card.boundingBox();
  await page.mouse.click(cardBounds.x + 20, cardBounds.y + cardBounds.height - 20);
  await expect(page).toHaveURL(new RegExp(`/Listings/Details\\?id=${bmwId}`, "i"));
});

test("listing details show one photo and an enlarged navigable viewer", async ({ page }) => {
  const bmwId = "10000000-0000-0000-0000-000000000002";
  await page.goto(`/Listings/Details?id=${bmwId}`);
  const gallery = page.locator("[data-detail-gallery]");
  const detailImages = gallery.locator("[data-detail-image]");
  const detailDots = gallery.locator("[data-detail-dot]");

  await expect(detailImages).toHaveCount(3);
  await expect(detailDots).toHaveCount(3);
  await expect(detailImages.nth(0)).toHaveClass(/is-active/);
  await gallery.locator("[data-detail-next]").click();
  await expect(detailImages.nth(1)).toHaveClass(/is-active/);
  await expect(gallery.locator("[data-detail-counter]")).toHaveText("2 / 3");

  await gallery.locator("[data-detail-open]").click();
  const viewer = page.locator("[data-photo-viewer]");
  await expect(viewer).toBeVisible();
  await expect(viewer.locator("[data-viewer-image]").nth(1)).toHaveClass(/is-active/);
  await viewer.locator("[data-viewer-next]").click();
  await expect(viewer.locator("[data-viewer-image]").nth(2)).toHaveClass(/is-active/);
  await expect(viewer.locator("[data-viewer-counter]")).toHaveText("3 / 3");
  await page.keyboard.press("ArrowRight");
  await expect(viewer.locator("[data-viewer-image]").nth(0)).toHaveClass(/is-active/);
  await page.keyboard.press("Escape");
  await expect(viewer).not.toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.locator("[data-detail-gallery]")).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
  await page.locator("[data-detail-open]").click();
  await expect(page.locator("[data-photo-viewer]")).toBeVisible();
  const mobileViewerBounds = await page.locator("[data-photo-viewer]").boundingBox();
  expect(Math.round(mobileViewerBounds.width)).toBe(390);
});
