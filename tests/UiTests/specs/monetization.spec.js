import { test, expect } from "@playwright/test";

async function signIn(page, email) {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}
async function signOut(page) { await page.goto("/Account/Profile"); await page.getByRole("button", { name: "Выйти", exact: true }).click(); }

test("sandbox plan, promo bonus and two-stage refund keep a financial audit", async ({ page }) => {
  const email = `sandbox-${Date.now()}@example.test`;
  await signIn(page, email);
  await page.goto("/Monetization");
  const advanced = page.locator(".product-grid article", { hasText: "Расширенный" });
  await advanced.locator("input[name=promoCode]").fill("WELCOME20");
  await advanced.getByRole("button", { name: "Купить в sandbox", exact: true }).click();
  await expect(page.getByText("Sandbox-оплата выполнена: 792 ₽.", { exact: true })).toBeVisible();
  await expect(page.getByText(/Лимит: 50 объявлений · бонусы: 100 ₽/)).toBeVisible();
  const payment = page.locator(".payment-list article", { hasText: "advanced · Succeeded" }).first();
  await payment.locator("input[name=reason]").fill("Проверка двухэтапного возврата");
  await payment.getByRole("button", { name: "Запросить возврат", exact: true }).click();
  await expect(page.getByText("Запрос на возврат передан финансовому оператору.", { exact: true })).toBeVisible();

  await signOut(page); await signIn(page, "admin@marketplace.local"); await page.goto("/Admin/Finance");
  const refund = page.locator(".refund-list article", { hasText: "Проверка двухэтапного возврата" }).first();
  await refund.locator("input[name=note]").fill("Оператор проверил sandbox-операцию");
  await refund.getByRole("button", { name: "Подготовить", exact: true }).click();
  const prepared = page.locator(".refund-list article", { hasText: "Проверка двухэтапного возврата" }).first();
  await prepared.locator("input[name=note]").fill("Контролёр подтвердил возврат");
  await prepared.getByRole("button", { name: "Подтвердить возврат", exact: true }).click();
  await expect(page.locator(".refund-list article", { hasText: "Approved" }).first()).toBeVisible();
  await page.goto("/Admin/Operations");
  await expect(page.locator(".admin-table tbody tr", { hasText: "finance.refund" }).first()).toBeVisible();
});
