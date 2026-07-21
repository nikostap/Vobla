import { test, expect } from "@playwright/test";

async function signIn(page, email) {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}

async function signOut(page) {
  await page.goto("/Account/Profile");
  await page.getByRole("button", { name: "Выйти", exact: true }).click();
}

test("chat persists messages and enforces links, attachment size, reports and blocks", async ({ page, browser }) => {
  const stamp = Date.now();
  const seller = `seller-${stamp}@example.test`;
  const buyer = `buyer-${stamp}@example.test`;
  const title = `Фотоаппарат для чата ${stamp}`;
  await signIn(page, seller);
  await page.goto("/Listings/Create");
  await page.getByLabel("Название", { exact: true }).fill(title);
  await page.getByLabel("Категория", { exact: true }).selectOption({ label: "Игровые ноутбуки" });
  await page.getByLabel("Цена", { exact: true }).fill("45000");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByLabel("Описание", { exact: true }).fill("Тестовое активное объявление для безопасного чата.");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByTestId("rules-consent").check({ force: true });
  await page.getByRole("button", { name: "Отправить на проверку", exact: true }).click();

  await signOut(page);
  await signIn(page, "admin@marketplace.local");
  await page.goto("/Admin/Moderation");
  const item = page.locator(".moderation-queue article", { hasText: title });
  await item.getByRole("link", { name: "Проверить", exact: true }).click();
  await page.getByLabel("Понятная причина", { exact: true }).fill("Объявление разрешено для теста чата.");
  await page.getByRole("button", { name: "Одобрить", exact: true }).click();

  await signOut(page);
  await signIn(page, buyer);
  await page.goto(`/?q=${encodeURIComponent(title)}`);
  await page.getByRole("heading", { name: title, exact: true }).getByRole("link").click();
  await page.getByRole("button", { name: "Написать продавцу", exact: true }).click();
  await expect(page.getByText("Диалог начат по объявлению.", { exact: true })).toBeVisible();
  await expect(page.locator(".chat-header h1 a")).toHaveText(title);
  await expect(page.locator(".chat-header h1 a")).toHaveAttribute("href", /\/Listings\/Details\?id=/i);
  await expect(page.locator(".chat-seller-link")).toHaveText(seller.split("@")[0]);
  await expect(page.locator(".chat-seller-link")).toHaveAttribute("href", /\/Sellers\/Details\?id=/i);
  await expect(page.locator(".chat-back-button")).toBeVisible();
  const chatUrl = page.url();

  const message = `Здравствуйте, товар актуален? ${stamp}`;
  await page.locator("[data-message-form] textarea").fill(message);
  await page.locator("[data-message-form]").evaluate(form => form.submit());
  await expect(page.getByText(message, { exact: true })).toBeVisible();
  await page.reload();
  await expect(page.getByText(message, { exact: true })).toBeVisible();

  const sellerContext = await browser.newContext({ baseURL: "http://127.0.0.1:5080" });
  const sellerPage = await sellerContext.newPage();
  await signIn(sellerPage, seller);
  await sellerPage.goto("/Messages");
  const sellerDialog = sellerPage.locator(".dialog-list > a", { hasText: title });
  await expect(sellerDialog.locator(".dialog-open-arrow")).toBeVisible();
  await sellerDialog.click();
  await expect(page.locator("[data-presence]")).toHaveText("в сети", { timeout: 10_000 });
  const realtimeMessage = `Realtime без перезагрузки ${stamp}`;
  await page.locator("[data-message-form] textarea").fill(realtimeMessage);
  await page.getByRole("button", { name: "Отправить", exact: true }).click();
  await expect(sellerPage.getByText(realtimeMessage, { exact: true })).toBeVisible({ timeout: 10_000 });
  await sellerContext.close();

  const shortcutMessage = `Отправлено через Ctrl+Enter ${stamp}`;
  await page.locator("[data-message-form] textarea").fill(shortcutMessage);
  await page.locator("[data-message-form] textarea").press("Control+Enter");
  await expect(page.getByText(shortcutMessage, { exact: true })).toBeVisible();

  await page.locator("[data-message-form] textarea").fill("Ссылка https://example.com запрещена");
  await page.locator("[data-message-form]").evaluate(form => form.submit());
  await expect(page.getByText("Внешние ссылки в чате запрещены.", { exact: true })).toBeVisible();

  const messageFiles = page.locator('[data-message-form] input[type="file"]');
  await messageFiles.setInputFiles({ name: "large.pdf", mimeType: "application/pdf", buffer: Buffer.alloc(20 * 1024 * 1024 + 1) });
  await page.locator("[data-message-form]").evaluate(form => form.submit());
  await expect(page.getByText(/Файл отклонён: до 20 МБ/)).toBeVisible();

  await messageFiles.setInputFiles({ name: "renamed.pdf", mimeType: "application/pdf", buffer: Buffer.from("<html><script>alert(1)</script></html>") });
  await page.locator("[data-message-form]").evaluate(form => form.submit());
  await expect(page.getByText(/Файл отклонён: до 20 МБ/)).toBeVisible();

  await messageFiles.setInputFiles({ name: "details.txt", mimeType: "text/plain", buffer: Buffer.from("Безопасное текстовое вложение", "utf8") });
  await page.locator("[data-message-form]").evaluate(form => form.submit());
  await expect(page.getByText("Файл отправлен вместе с сообщением.", { exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: /details\.txt/ })).toBeVisible();

  const tinyPng = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", "base64");
  await messageFiles.setInputFiles({ name: "camera.png", mimeType: "image/png", buffer: tinyPng });
  await page.locator("[data-message-form] textarea").fill("Фото товара");
  await page.getByRole("button", { name: "Отправить", exact: true }).click();
  const inlineImage = page.getByRole("button", { name: "Открыть изображение camera.png", exact: true });
  await expect(inlineImage.locator("img")).toBeVisible();
  await inlineImage.click();
  await expect(page.locator("[data-chat-image-viewer]")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator("[data-chat-image-viewer]")).not.toBeVisible();

  await page.locator('form[action*="Report"] input[name="reason"]').fill("Подозрительное поведение");
  await page.getByRole("button", { name: "Пожаловаться", exact: true }).click();
  await expect(page.getByText("Жалоба отправлена на проверку.", { exact: true })).toBeVisible();

  await signOut(page);
  await signIn(page, seller);
  await page.goto("/Messages");
  const dialog = page.locator(".dialog-list > a", { hasText: title });
  await expect(dialog).toContainText("Фото товара");
  await dialog.click();
  await expect(page.getByText(message, { exact: true })).toBeVisible();
  await expect(page.getByText(realtimeMessage, { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Заблокировать", exact: true }).click();
  await expect(page.getByText("Обмен сообщениями заблокирован.", { exact: true })).toBeVisible();
  await page.goto(chatUrl);
  await expect(page.getByText("Обмен сообщениями заблокирован.", { exact: true })).toBeVisible();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  await expect(page.locator(".chat-back-button")).toBeVisible();
  await expect(page.locator(".chat-header h1 a")).toHaveAttribute("href", /\/Listings\/Details\?id=/i);
  expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
  const sellerHref = await page.locator(".chat-seller-link").getAttribute("href");
  await page.goto(sellerHref);
  await expect(page.locator(".seller-profile h1")).toHaveText(seller.split("@")[0]);
  await expect(page.locator(".seller-verified")).toBeVisible();
});
