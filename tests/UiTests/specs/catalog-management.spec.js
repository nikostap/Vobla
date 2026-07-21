import { test, expect } from "@playwright/test";

async function signInAsAdmin(page) {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill("admin@marketplace.local");
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}

test("managed catalog exposes all roots and dynamic schemas for deep demo branches", async ({ page }) => {
  await page.goto("/Catalog");
  expect(await page.locator(".directory-tree > details").count()).toBeGreaterThanOrEqual(15);
  for (const name of ["Транспорт", "Недвижимость", "Электроника", "Дом, мебель и интерьер"]) {
    await expect(page.getByRole("link", { name, exact: true })).toBeVisible();
  }

  const branches = [
    ["prodazha-kvartir", "Продажа квартир", "Количество комнат"],
    ["sedany", "Седаны", "Марка"],
    ["igrovye-noutbuki", "Игровые ноутбуки", "Оперативная память"],
    ["divany", "Диваны", "Материал"]
  ];
  for (const [slug, heading, field] of branches) {
    await page.goto(`/Catalog?slug=${slug}`);
    await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
    await expect(page.getByLabel(field, { exact: true })).toBeVisible();
  }
});

test("administrator changes the catalog without application rebuild", async ({ page }) => {
  const suffix = Date.now();
  const name = `Тестовая категория ${suffix}`;
  const slug = `test-category-${suffix}`;
  await signInAsAdmin(page);
  await page.goto("/Admin/Catalog");
  await expect(page.getByRole("heading", { name: "Редактор каталога", exact: true })).toBeVisible();

  await page.getByLabel("Название", { exact: true }).fill(name);
  await page.getByLabel("Технический код", { exact: true }).fill(slug);
  await page.getByLabel("Родительская категория", { exact: true }).selectOption({ label: "Услуги" });
  await page.getByRole("button", { name: "Добавить категорию", exact: true }).click();
  await expect(page.getByText(`Категория «${name}» добавлена.`)).toBeVisible();

  await page.goto(`/Catalog?slug=${slug}`);
  await expect(page.getByRole("link", { name, exact: true })).toBeVisible();
});

test("administrator can publish a new schema version", async ({ page }) => {
  await signInAsAdmin(page);
  await page.goto("/Admin/Catalog");
  const notebookRow = page.locator(".admin-category-list > div", { hasText: "Игровые ноутбуки" });
  await notebookRow.getByRole("button", { name: "Новая версия", exact: true }).click();
  await expect(page.locator(".notice")).toContainText("схемы создана.");

  await page.goto("/Catalog?slug=igrovye-noutbuki");
  await expect(page.getByText("Версия схемы", { exact: false })).toBeVisible();
  await expect(page.getByLabel("Оперативная память", { exact: true })).toBeVisible();
});
