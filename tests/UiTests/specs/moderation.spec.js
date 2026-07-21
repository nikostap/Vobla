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

test("critical rule, moderator decision and owner appeal are traceable", async ({ page }) => {
  const stamp = Date.now();
  const email = `moderation-${stamp}@example.test`;
  const title = `Коллекционное оружие ${stamp}`;
  const reason = `Запрещённый товар: точная причина ${stamp}`;
  await signIn(page, email);

  await page.goto("/Listings/Create");
  await page.getByLabel("Название", { exact: true }).fill(title);
  await page.getByLabel("Категория", { exact: true }).selectOption({ label: "Седаны" });
  await page.getByLabel("Цена", { exact: true }).fill("1000");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByLabel("Описание", { exact: true }).fill("Тест автоматического критического правила.");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByTestId("rules-consent").check({ force: true });
  await page.getByRole("button", { name: "Отправить на проверку", exact: true }).click();
  await expect(page.getByText("Объявление помещено в карантин и ожидает проверки.")).toBeVisible();
  const listing = page.locator(".my-listings article", { hasText: title });
  await expect(listing.getByText("Карантин", { exact: true })).toBeVisible();
  await listing.getByRole("link", { name: "Открыть", exact: true }).click();
  const detailUrl = page.url();

  await signOut(page);
  await signIn(page, "admin@marketplace.local");
  await page.goto("/Admin/Moderation");
  const queueItem = page.locator(".moderation-queue article", { hasText: title });
  await expect(queueItem.getByText("Critical", { exact: true })).toBeVisible();
  await expect(queueItem.getByText("demo-1.0", { exact: true })).toBeVisible();
  await queueItem.getByRole("link", { name: "Проверить", exact: true }).click();
  await expect(page.getByText("Скрыт политикой доступа", { exact: true })).toBeVisible();
  await expect(page.locator(".finding-list")).toContainText("PROHIBITED_GOODS · Critical");
  await expect(page.locator(".finding-list")).toContainText("DEMO_PROHIBITED_GOODS v1.0");
  await page.getByLabel("Понятная причина", { exact: true }).fill(reason);
  await page.getByLabel("Проблемное поле", { exact: true }).fill("Название");
  await page.getByLabel("Ссылка на правило", { exact: true }).fill("rules/prohibited-goods");
  await page.getByLabel("Как исправить", { exact: true }).fill("Удалите запрещённый товар и создайте допустимое объявление.");
  await page.getByRole("button", { name: "Отклонить", exact: true }).click();
  await expect(page.locator(".moderation-queue article", { hasText: title })).toHaveCount(0);

  await signOut(page);
  await signIn(page, email);
  await page.goto(detailUrl);
  const decision = page.getByTestId("moderation-decision");
  await expect(decision).toContainText(reason);
  await expect(decision).toContainText("rules/prohibited-goods");
  await expect(decision).toContainText("Удалите запрещённый товар");
  await page.getByLabel("Не согласны с решением?", { exact: true }).fill("Товар имеет историческую ценность, прошу пересмотреть решение.");
  await page.getByRole("button", { name: "Подать апелляцию", exact: true }).click();
  await expect(page.getByText("Апелляция отправлена модератору.", { exact: true })).toBeVisible();
  await expect(page.getByText("Апелляция ожидает рассмотрения.", { exact: true })).toBeVisible();
  await signOut(page);
  await signIn(page, "admin@marketplace.local");
  await page.goto("/Admin/Moderation");
  const appealItem = page.locator(".moderation-queue article", { hasText: title });
  await expect(appealItem.getByText("Апелляция", { exact: true })).toBeVisible();
  await appealItem.getByRole("link", { name: "Проверить", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Апелляция пользователя", exact: true })).toBeVisible();
  await expect(page.getByText("Товар имеет историческую ценность, прошу пересмотреть решение.", { exact: true })).toBeVisible();
});
