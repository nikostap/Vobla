import { test, expect } from "@playwright/test";

async function signIn(page, email) {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}

test("saved searches and recommendation history remain under user control", async ({ page }) => {
  const stamp = Date.now();
  await signIn(page, `recommendations-${stamp}@example.test`);

  await page.goto(`/?q=${encodeURIComponent("ноутбук")}&minPrice=1000`);
  await page.getByRole("button", { name: "Сохранить поиск", exact: true }).click();
  await expect(page.getByText("Поиск сохранён.", { exact: false })).toBeVisible();
  await expect(page.getByRole("button", { name: "Поиск сохранён", exact: true })).toBeDisabled();

  const viewedTitle = await page.locator(".listing-card__link").first().textContent();
  await page.locator(".listing-card__link").first().click();
  await page.goto("/Listings/My");
  await expect(page.getByRole("heading", { name: "Недавно просмотренные", exact: true })).toBeVisible();
  await expect(page.locator(".recently-viewed-grid").getByText(viewedTitle.trim(), { exact: true })).toBeVisible();

  await page.goto("/Recommendations");
  await expect(page.getByRole("heading", { name: "Рекомендации и сохранённые поиски", exact: true })).toBeVisible();
  await expect(page.locator(".saved-search-item")).toHaveCount(1);
  await page.getByRole("button", { name: "Уведомления включены", exact: true }).click();
  await expect(page.getByRole("button", { name: "Уведомления выключены", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Очистить историю", exact: true }).click();
  await expect(page.getByText("История просмотров и поиска очищена.", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Удалить", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Поисков пока нет", exact: true })).toBeVisible();
});
