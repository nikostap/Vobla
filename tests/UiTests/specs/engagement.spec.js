import { test, expect } from "@playwright/test";

async function signIn(page, email) {
  await page.context().clearCookies();
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}

async function signOut(page) { await page.goto("/Account/Profile"); await page.getByRole("button", { name: "Выйти", exact: true }).click(); }

test("favorites, comparison, confirmed deal, price alert and verified reviews", async ({ page }) => {
  test.setTimeout(60_000);
  const stamp = Date.now(); const seller = `deal-seller-${stamp}@example.test`; const buyer = `deal-buyer-${stamp}@example.test`; const title = `Ноутбук для сделки ${stamp}`;
  await signIn(page, seller);
  await page.goto("/Listings/Create");
  await page.getByLabel("Название", { exact: true }).fill(title);
  await page.getByLabel("Категория", { exact: true }).selectOption({ label: "Игровые ноутбуки" });
  await page.getByLabel("Цена", { exact: true }).fill("100000");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByLabel("Описание", { exact: true }).fill("Проверка избранного, сделки и подтверждённых отзывов.");
  await page.getByLabel("Процессор", { exact: true }).fill("Ryzen 9");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByTestId("rules-consent").check({ force: true }); await page.getByRole("button", { name: "Отправить на проверку", exact: true }).click();
  await page.locator(".my-listings article", { hasText: title }).getByRole("link", { name: "Открыть" }).click(); const detailUrl = page.url();

  await signOut(page); await signIn(page, "admin@marketplace.local"); await page.goto("/Admin/Moderation"); const queue = page.locator(".moderation-queue article", { hasText: title }); await queue.getByRole("link", { name: "Проверить" }).click(); await page.getByLabel("Понятная причина", { exact: true }).fill("Допустимое объявление для подтверждённой сделки."); await page.getByRole("button", { name: "Одобрить", exact: true }).click();

  await signOut(page); await signIn(page, buyer); await page.goto(detailUrl);
  await page.getByRole("button", { name: "В избранное", exact: true }).click();
  await page.getByRole("button", { name: "Сравнить", exact: true }).click();
  await page.goto("/Favorites"); await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
  await page.goto("/Compare"); await expect(page.getByRole("link", { name: title, exact: true })).toBeVisible(); await expect(page.getByText("Ryzen 9", { exact: true })).toBeVisible();
  await page.goto(detailUrl); await page.getByRole("button", { name: "Написать продавцу", exact: true }).click(); const chatUrl = page.url();
  await expect(page.getByRole("button", { name: "Я купил товар", exact: true })).toBeVisible(); await expect(page.getByRole("button", { name: "Оставить подтверждённый отзыв" })).toHaveCount(0);
  await page.getByRole("button", { name: "Я купил товар", exact: true }).click(); await expect(page.getByText("Покупатель сообщил о покупке", { exact: false })).toBeVisible();

  await signOut(page); await signIn(page, seller); await page.goto("/Messages"); await page.locator(".dialog-list > a", { hasText: title }).click();
  await page.getByRole("button", { name: "Подтвердить покупателя", exact: true }).click(); await expect(page.getByText("Сделка подтверждена сторонами", { exact: false })).toBeVisible();
  await page.locator(".review-form select[name=rating]").selectOption("5"); await page.locator(".review-form textarea[name=reviewText]").fill("Надёжный покупатель."); await page.getByRole("button", { name: "Оставить подтверждённый отзыв", exact: true }).click(); await expect(page.getByText("Отзыв опубликован как подтверждённый сделкой.", { exact: true })).toBeVisible();

  await page.goto(detailUrl); await page.getByRole("link", { name: "Редактировать", exact: true }).click(); await page.getByLabel("Цена", { exact: true }).fill("90000"); await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();

  await signOut(page); await signIn(page, buyer); await page.goto(chatUrl); await expect(page.getByText("Completed", { exact: false })).toBeVisible();
  await page.locator(".review-form select[name=rating]").selectOption("5"); await page.locator(".review-form textarea[name=reviewText]").fill("Сделка прошла отлично."); await page.getByRole("button", { name: "Оставить подтверждённый отзыв", exact: true }).click(); await expect(page.getByText("Отзыв опубликован как подтверждённый сделкой.", { exact: true })).toBeVisible();
  await page.goto("/Notifications");
  const priceNotification = page.locator(".notification-item", { hasText: /Цена снижена: .*90,000 ₽/ });
  await expect(priceNotification).toBeVisible();
  await expect(priceNotification).toHaveClass(/is-unread/);
  await priceNotification.getByRole("button", { name: "Открыть", exact: true }).click();
  await expect(page).toHaveURL(detailUrl);
});

test("guest actions return to the listing and connect seller, favorites and comparison", async ({ page }) => {
  const listingId = "10000000-0000-0000-0000-000000000002";
  const detailUrl = `/Listings/Details?id=${listingId}`;
  await page.context().clearCookies();
  await page.goto(detailUrl);

  const sellerLink = page.locator(".listing-seller-summary");
  await expect(sellerLink).toBeVisible();
  await expect(sellerLink).toHaveAttribute("href", /\/Sellers\/Details\?id=/);
  await expect(page.getByRole("link", { name: "Войти, чтобы написать", exact: true })).toBeVisible();
  await expect(page.getByRole("link", { name: "Войти и сравнить", exact: true })).toBeVisible();

  await page.getByRole("link", { name: "Войти и добавить в избранное", exact: true }).click();
  await expect(page).toHaveURL(/\/Account\/SignIn\?ReturnUrl=.*intent%3Dfavorite/i);
  const stamp = Date.now();
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(`guest-flow-${stamp}@example.test`);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();

  await expect(page).toHaveURL(new RegExp(`${listingId}.*intent=favorite`));
  await expect(page.getByText("Вы вошли в аккаунт. Завершите выбранное действие ниже.", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "В избранное", exact: true }).click();
  await expect(page.getByText("Объявление добавлено в избранное.", { exact: true })).toBeVisible();

  await page.getByRole("button", { name: "Сравнить", exact: true }).click();
  await expect(page.getByText("Объявление добавлено к сравнению.", { exact: true })).toBeVisible();
  await page.goto("/Favorites");
  await expect(page.getByRole("link", { name: /BMW 5 серия/i })).toBeVisible();
  await page.goto("/Compare");
  await expect(page.getByRole("link", { name: /BMW 5 серия/i })).toBeVisible();
  await expect(page.getByText("Добавьте ещё одно объявление этой категории", { exact: false })).toBeVisible();

  await page.goto(detailUrl);
  const sellerHref = await page.locator(".listing-seller-summary").getAttribute("href");
  await page.locator(".listing-seller-summary").click();
  await expect(page).toHaveURL(sellerHref);
  await expect(page.getByRole("heading", { name: "Объявления продавца", exact: true })).toBeVisible();
});
