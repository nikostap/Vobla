import { test, expect } from "@playwright/test";

async function signIn(page, email) {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код", exact: true }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByRole("textbox", { name: "Одноразовый код", exact: true }).fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться", exact: true }).click();
}

test("administrative platform enforces RBAC and audits operational changes", async ({ page }) => {
  const memberEmail = `member-admin-check-${Date.now()}@example.test`;
  await signIn(page, memberEmail);
  await page.goto("/Admin");
  await expect(page).toHaveURL(/Account\/AccessDenied/);
  await expect(page.getByRole("heading", { name: "Недостаточно прав", exact: true })).toBeVisible();
  await page.goto("/Account/Data");
  await page.getByRole("checkbox", { name: /Я понимаю/ }).check();
  await page.getByRole("button", { name: "Запросить удаление учётной записи" }).click();
  await expect(page.getByText(/Запрос зарегистрирован/)).toBeVisible();
  await page.goto("/Account/Profile");
  await page.getByRole("button", { name: "Выйти", exact: true }).click();

  await signIn(page, "admin@marketplace.local");
  await page.goto("/Admin");
  await expect(page.getByRole("heading", { name: "Административная платформа", exact: true })).toBeVisible();
  await expect(page.locator(".admin-metrics article")).toHaveCount(7);
  await page.getByRole("link", { name: "Пользователи и роли", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Пользователи и роли", exact: true })).toBeVisible();
  await page.getByLabel("Email или имя пользователя", { exact: true }).fill(memberEmail);
  await page.getByRole("button", { name: "Найти", exact: true }).click();
  let memberRow = page.locator(".admin-table tbody tr", { hasText: memberEmail });
  await expect(memberRow).toBeVisible();
  await memberRow.getByPlaceholder("Например: принят в команду модерации").fill("Назначение для проверки управления ролями");
  await memberRow.getByRole("button", { name: "Назначить модератором", exact: true }).click();
  await expect(page.getByText("назначен модератором", { exact: false })).toBeVisible();
  memberRow = page.locator(".admin-table tbody tr", { hasText: memberEmail });
  await expect(memberRow.getByText("Moderator", { exact: true })).toBeVisible();
  await memberRow.getByPlaceholder("Например: принят в команду модерации").fill("Снятие роли после проверки интерфейса");
  await memberRow.getByRole("button", { name: "Снять роль модератора", exact: true }).click();
  await expect(page.getByText("Роль модератора снята", { exact: false })).toBeVisible();

  await page.getByRole("link", { name: "Приватность", exact: true }).click();
  const privacyRequest = page.locator(".admin-table tbody tr", { hasText: memberEmail });
  await expect(privacyRequest).toBeVisible();
  const decisionStatuses = await privacyRequest.locator("form").evaluate(async form => {
    const submit = async (approve, note) => {
      const body = new URLSearchParams(new FormData(form));
      body.set("approve", String(approve));
      body.set("note", note);
      const response = await fetch(form.action, { method: "POST", body });
      return response.status;
    };
    return Promise.all([submit(true, "Первое конкурентное решение"), submit(false, "Второе конкурентное решение")]);
  });
  expect(decisionStatuses.sort()).toEqual([200, 409]);
  await page.reload();
  await expect(privacyRequest).toHaveCount(0);

  await page.goto("/Admin/Operations");
  const flag = page.locator(".flag-list article").first();
  await flag.locator("input[name=reason]").fill("Проверка аудита feature flag");
  await flag.getByRole("button").click();
  await expect(page.locator(".admin-table tbody tr", { hasText: "admin.feature-flag" }).first()).toBeVisible();
  await expect(page.locator(".admin-table tbody tr", { hasText: "admin.privacy.erasure-resolved" }).first()).toBeVisible();
  await page.getByRole("button", { name: "Очистить историю 30+", exact: true }).click();
  await expect(page.locator(".admin-table tbody tr", { hasText: "admin.job.queued" }).first()).toBeVisible();
  await expect.poll(async () => { await page.reload(); return await page.locator(".history-list li", { hasText: "history-retention · Completed" }).count(); }, { timeout: 15_000 }).toBeGreaterThan(0);
});
