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

test("database search tolerates a typo and restores URL filters", async ({ page }) => {
  const stamp = Date.now();
  const title = `Платиновый ультрабук ${stamp}`;
  await signIn(page, `search-${stamp}@example.test`);
  await page.goto("/Listings/Create");
  await page.getByLabel("Название", { exact: true }).fill(title);
  await page.getByLabel("Категория", { exact: true }).selectOption({ label: "Игровые ноутбуки" });
  await page.getByLabel("Цена", { exact: true }).fill("125000");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByLabel("Описание", { exact: true }).fill("Редкая производительная модель для работы.");
  await page.getByRole("button", { name: "Сохранить и продолжить", exact: true }).click();
  await page.getByTestId("rules-consent").check({ force: true });
  await page.getByRole("button", { name: "Отправить на проверку", exact: true }).click();

  await signOut(page);
  await signIn(page, "admin@marketplace.local");
  await page.goto("/Admin/Moderation");
  const item = page.locator(".moderation-queue article", { hasText: title });
  await item.getByRole("link", { name: "Проверить", exact: true }).click();
  await page.getByLabel("Понятная причина", { exact: true }).fill("Объявление соответствует правилам.");
  await page.getByRole("button", { name: "Одобрить", exact: true }).click();

  await page.goto(`/?q=${encodeURIComponent("Платиновы")}&minPrice=120000&maxPrice=130000&sort=price-desc`);
  await expect(page.getByText("база данных", { exact: false })).toBeVisible();
  await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
  await expect(page.getByLabel("Сортировка")).toHaveValue("price-desc");
  await page.locator('[data-dialog-open="filter-dialog"]').click();
  await expect(page.getByLabel("Цена от")).toHaveValue("120000");
  await expect(page.getByLabel("Цена до", { exact: true })).toHaveValue("130000");
  await page.getByRole("button", { name: "Закрыть", exact: true }).click();
  await page.reload();
  await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();
  await expect(page).toHaveURL(/minPrice=120000/);

  await page.goto(`/?q=${encodeURIComponent("абракадабрасверхредкаяфраза")}`);
  await expect(page.getByRole("heading", { name: "Ничего не найдено", exact: true })).toBeVisible();
  await expect(page.getByText("Попробуйте убрать часть фильтров", { exact: false })).toBeVisible();
});
