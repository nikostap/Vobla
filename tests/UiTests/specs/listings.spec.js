import { test, expect } from "@playwright/test";

async function signIn(page) {
  const email = `listing-${Date.now()}@example.test`;
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}

test("user creates a three-step listing and sees it in My Listings", async ({ page }) => {
  await signIn(page);
  await page.goto("/Listings/Create");
  await page.getByLabel("Название", { exact: true }).fill("Тестовый игровой ноутбук");
  await page.getByLabel("Категория", { exact: true }).selectOption({ label: "Игровые ноутбуки" });
  await page.getByLabel("Цена", { exact: true }).fill("85000");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await expect(page.getByLabel("Описание", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Процессор", { exact: true })).toHaveCount(1);
  await expect(page.getByLabel("Адрес или ориентир", { exact: true })).toBeVisible();

  await page.getByLabel("Описание", { exact: true }).fill("Ноутбук в отличном состоянии, полный комплект.");
  await page.getByLabel("Процессор", { exact: true }).fill("Intel Core i7");
  await page.getByLabel("Адрес или ориентир", { exact: true }).fill("Тверская улица");
  await expect(page.locator("#listing-address-suggestions [role=option]").first()).toBeVisible({ timeout: 10_000 });
  await page.locator("#listing-address-suggestions [role=option]").first().click();
  await expect(page.locator('[name="Input.Latitude"]')).not.toHaveValue("");
  await page.getByLabel("Фотографии", { exact: true }).setInputFiles("../../src/Marketplace.Web/wwwroot/demo/images/iphone.png");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await expect(page.getByText("Предпросмотр", { exact: true })).toBeVisible();
  const previewImage = page.getByLabel("Загруженные фотографии").locator("img").first();
  await expect(previewImage).toBeVisible();
  await expect.poll(() => previewImage.evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
  const uploadedPhotoUrl = await previewImage.getAttribute("src");
  const uploadedPhotoResponse = await page.request.get(uploadedPhotoUrl);
  expect(uploadedPhotoResponse.status()).toBe(200);
  expect(uploadedPhotoResponse.headers()["content-type"]).toBe("image/png");
  await page.getByTestId("rules-consent").check({ force: true });
  await page.getByRole("button", { name: "Отправить на проверку", exact: true }).click();

  await expect(page.getByText("Объявление отправлено на автоматическую проверку.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Тестовый игровой ноутбук", exact: true })).toBeVisible();
  await expect(page.getByText("На проверке", { exact: true })).toBeVisible();

  await page.getByRole("link", { name: "Открыть", exact: true }).click();
  const detailUrl = page.url();
  const detailImage = page.locator(".listing-gallery__image.is-active");
  await expect(detailImage).toBeVisible();
  await expect.poll(() => detailImage.evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
  await expect(page.getByText("Intel Core i7", { exact: true })).toBeVisible();
  await expect(page.getByText("Ревизия 1", { exact: true })).toBeVisible();

  await page.getByRole("link", { name: "Редактировать", exact: true }).click();
  await page.getByLabel("Название", { exact: true }).fill("Обновлённый игровой ноутбук");
  await page.getByLabel("Цена", { exact: true }).fill("80000");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.goto(detailUrl);
  await expect(page.getByRole("heading", { name: "Обновлённый игровой ноутбук", exact: true })).toBeVisible();
  await expect(page.locator(".detail-price")).toContainText("80");
  await expect(page.getByText("Ревизия 2", { exact: true })).toBeVisible();
  const priceHistory = page.locator(".history-grid .detail-card", { hasText: "История цены" });
  await expect(priceHistory.getByText(/85(?:,|[\s\u00a0\u202f])000 ₽/)).toBeVisible();
  await expect(priceHistory.getByText(/80(?:,|[\s\u00a0\u202f])000 ₽/)).toBeVisible();

  const originalId = new URL(detailUrl).searchParams.get("id");
  await page.getByRole("link", { name: "Редактировать", exact: true }).click();
  await page.getByLabel("Категория", { exact: true }).selectOption({ label: "Седаны" });
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  const recreatedId = new URL(page.url()).searchParams.get("id");
  expect(recreatedId).not.toBe(originalId);
  await page.goto("/Listings/My");
  await expect(page.getByText("Черновик", { exact: true })).toBeVisible();
  await expect(page.getByText("Archived", { exact: true })).toBeVisible();
  const draft = page.locator(".my-listings article", { hasText: "Черновик" });
  const draftUrl = await draft.getByRole("link", { name: "Открыть", exact: true }).getAttribute("href");
  await draft.locator("summary").click();
  await draft.getByRole("checkbox", { name: "Подтверждаю удаление", exact: true }).check();
  await draft.getByRole("button", { name: "Удалить объявление", exact: true }).click();
  await expect(page.getByText(/Объявление «.*» удалено/)).toBeVisible();
  await expect(page.getByText("Черновик", { exact: true })).toHaveCount(0);
  const deletedResponse = await page.goto(draftUrl);
  expect(deletedResponse.status()).toBe(404);
});
