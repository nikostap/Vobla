import { test, expect } from "@playwright/test";

test("email code signs in, saves profile and exposes the current session", async ({ page }) => {
  await page.route("**/maps-api/api/v1/geo/suggest**", async route => route.fulfill({
    contentType: "application/json",
    body: JSON.stringify([{ displayName: "Владивосток, Светланская улица, 10, Приморский край, Россия", latitude: 43.115542, longitude: 131.885494 }])
  }));
  const email = `tester-${Date.now()}@example.test`;
  await page.goto("/Account/SignIn");
  await expect(page.getByRole("heading", { name: "Войти или создать аккаунт", exact: true })).toBeVisible();
  await expect(page.getByText("Если такого email ещё нет, аккаунт создастся автоматически.", { exact: false })).toBeVisible();
  await expect(page.getByText("fake-provider", { exact: false })).toHaveCount(0);
  await page.getByRole("textbox", { name: "Email", exact: true }).fill(email);
  await page.getByRole("button", { name: "Получить код" }).click();

  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByLabel("Одноразовый код").fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться" }).click();

  await expect(page.getByRole("heading", { name: "Профиль и приватность" })).toBeVisible();
  const profileHeaderGeometry = await page.evaluate(() => {
    const logo = document.querySelector(".account-header .brand-mark").getBoundingClientRect();
    const navigation = document.querySelector(".account-nav").getBoundingClientRect();
    return { logoTop: logo.top, logoCenter: logo.top + logo.height / 2, navigationCenter: navigation.top + navigation.height / 2 };
  });
  await page.goto("/");
  const homeHeaderGeometry = await page.evaluate(() => {
    const logo = document.querySelector(".site-header .brand-mark").getBoundingClientRect();
    const navigation = document.querySelector(".user-actions").getBoundingClientRect();
    return { logoTop: logo.top, logoCenter: logo.top + logo.height / 2, navigationCenter: navigation.top + navigation.height / 2 };
  });
  expect(Math.abs(profileHeaderGeometry.logoTop - homeHeaderGeometry.logoTop)).toBeLessThanOrEqual(1);
  expect(Math.abs(profileHeaderGeometry.logoCenter - profileHeaderGeometry.navigationCenter)).toBeLessThanOrEqual(1);
  expect(Math.abs(homeHeaderGeometry.logoCenter - homeHeaderGeometry.navigationCenter)).toBeLessThanOrEqual(1);
  const profileResponse = await page.goto("/Account/Profile");
  expect(profileResponse.headers()["cache-control"]).toContain("no-store");
  const cookies = await page.context().cookies();
  const sessionCookie = cookies.find(cookie => cookie.name === "marketplace.session");
  expect(sessionCookie).toMatchObject({ httpOnly: true, secure: false, sameSite: "Lax", path: "/" });
  const csrfCookie = cookies.find(cookie => cookie.name === "marketplace.csrf");
  expect(csrfCookie).toMatchObject({ httpOnly: true, secure: false, sameSite: "Strict", path: "/" });
  await expect(page.getByLabel("Отображаемое имя")).toHaveValue(email.split("@")[0]);

  await page.getByLabel("Отображаемое имя").fill("Тестовый профиль");
  await page.getByLabel("Адрес", { exact: true }).fill("Владивосток Светланская 10");
  await expect(page.locator("#profile-address-suggestions [role=option]")).toHaveCount(1);
  await page.getByRole("option", { name: "Владивосток, Светланская улица, 10, Приморский край, Россия", exact: true }).click();
  await expect(page.getByLabel("Адрес", { exact: true })).toHaveValue("Владивосток, Светланская улица, 10, Приморский край, Россия");
  await expect(page.locator('[name="Input.CityLatitude"]')).not.toHaveValue("");
  await expect(page.locator('[name="Input.CityLongitude"]')).not.toHaveValue("");
  await page.locator("[data-avatar-file]").setInputFiles("../../src/Marketplace.Web/wwwroot/demo/images/iphone.png");
  await expect(page.getByRole("heading", { name: "Кадрирование аватара" })).toBeVisible();
  await expect(page.locator("[data-avatar-zoom]")).toHaveAttribute("min", "0.35");
  await page.locator("[data-avatar-zoom]").fill("0.5");
  await page.getByRole("button", { name: "Применить" }).click();
  await page.getByRole("button", { name: "Сохранить изменения" }).click();
  await expect(page.getByText("Профиль и настройки приватности сохранены.")).toBeVisible();
  await expect(page.getByLabel("Адрес", { exact: true })).toHaveValue("Владивосток, Светланская улица, 10, Приморский край, Россия");
  await expect(page.locator(".profile-avatar img")).toBeVisible();
  await expect.poll(() => page.locator(".profile-avatar img").evaluate(image => image.complete && image.naturalWidth > 0)).toBe(true);
  const avatarUrl = await page.locator(".profile-avatar img").getAttribute("src");
  const avatarResponse = await page.request.get(avatarUrl);
  expect(avatarResponse.status()).toBe(200);
  expect(avatarResponse.headers()["content-type"]).toBe("image/jpeg");
  await expect(page.getByRole("link", { name: "Удалить учётную запись", exact: true })).toHaveAttribute("href", "/Account/Data#delete-account");

  await page.getByRole("link", { name: "Управлять сессиями" }).click();
  await expect(page.getByRole("heading", { name: "Активные сессии" })).toBeVisible();
  await expect(page.getByText("Текущая")).toBeVisible();

  await expect(page.getByRole("link", { name: "Роли" })).toHaveCount(0);

  await page.getByRole("link", { name: "Данные", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Данные и удаление аккаунта" })).toBeVisible();
  const exported = await page.evaluate(async () => {
    const response = await fetch("/Account/Data?handler=Export");
    return {
      status: response.status,
      cacheControl: response.headers.get("cache-control"),
      disposition: response.headers.get("content-disposition"),
      data: await response.json()
    };
  });
  expect(exported.status).toBe(200);
  expect(exported.cacheControl).toContain("no-store");
  expect(exported.disposition).toContain("attachment");
  expect(exported.data.schemaVersion).toBe(1);
  expect(exported.data.profile.email).toBe(email);
  expect(exported.data.profile.displayName).toBe("Тестовый профиль");
  expect(exported.data.profile.address).toBe("Владивосток, Светланская улица, 10, Приморский край, Россия");
  expect(exported.data.roles).toContain("Member");
  expect(exported.data.sessions.length).toBeGreaterThan(0);
  const serialized = JSON.stringify(exported.data).toLowerCase();
  expect(serialized).not.toContain("passwordhash");
  expect(serialized).not.toContain("securitystamp");
  expect(serialized).not.toContain("debugcode");
  expect(serialized).not.toContain("storagekey");

  const repeatedExports = await page.evaluate(async () => {
    const responses = [];
    for (let index = 0; index < 5; index++) {
      const response = await fetch("/Account/Data?handler=Export");
      responses.push({ status: response.status, retryAfter: response.headers.get("retry-after") });
    }
    return responses;
  });
  expect(repeatedExports.slice(0, 4).every(item => item.status === 200)).toBeTruthy();
  expect(repeatedExports[4].status).toBe(429);
  expect(Number(repeatedExports[4].retryAfter)).toBeGreaterThan(0);

  await page.getByRole("checkbox", { name: /Я понимаю/ }).check();
  await page.getByRole("button", { name: "Запросить удаление учётной записи" }).click();
  await expect(page.getByText(/Запрос зарегистрирован/)).toBeVisible();
  const cancellationStatuses = await page.locator('form[action*="CancelErasure"]').evaluate(async form => {
    const submit = async () => (await fetch(form.action, { method: "POST", body: new URLSearchParams(new FormData(form)) })).status;
    return Promise.all([submit(), submit()]);
  });
  expect(cancellationStatuses.sort()).toEqual([200, 409]);
  await page.reload();
  await expect(page.getByRole("button", { name: "Запросить удаление учётной записи" })).toBeVisible();
});

test("reserved demo admin address receives administrator role", async ({ page }) => {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill("admin@marketplace.local");
  await page.getByRole("button", { name: "Получить код" }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByLabel("Одноразовый код").fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться" }).click();
  await page.getByRole("link", { name: "Роли" }).click();
  const admin = page.locator(".role-card", { hasText: "Администратор" });
  await expect(admin).toContainText("Назначена");
});

test("russian phone number can create an account with a one-time code", async ({ page }) => {
  await page.goto("/Account/SignIn");
  await page.getByRole("textbox", { name: "Email", exact: true }).fill("+7 (999) 123-45-67");
  await page.getByRole("button", { name: "Получить код" }).click();
  const code = (await page.getByTestId("demo-code").locator("strong").textContent()).trim();
  await page.getByLabel("Одноразовый код").fill(code);
  await page.getByRole("button", { name: "Войти или зарегистрироваться" }).click();
  await expect(page.getByRole("heading", { name: "Профиль и приватность" })).toBeVisible();
  await expect(page.getByText("+7 (999) 123-45-67", { exact: false })).toBeVisible();
});
