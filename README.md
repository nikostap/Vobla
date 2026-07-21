# Marketplace

Рабочий прототип российского сервиса частных объявлений. Завершён полный delivery plan **M0–M13**: UI-shell, Identity, каталог, объявления, модерация, поиск, география, realtime-чат, сделки, рекомендации, административная платформа, sandbox-монетизация и hardening закрытой беты.

## Запуск одной командой

Требуется Docker Desktop:

```powershell
docker compose up --build
```

Открыть: <http://localhost:5080>  
Health endpoint: <http://localhost:5080/health>

Для оркестратора: независимый liveness — <http://localhost:5080/health/live>, готовность с проверкой PostgreSQL — <http://localhost:5080/health/ready>.

Для быстрого запуска без Docker требуется .NET SDK 10.0.300:

```powershell
dotnet run --project src/Marketplace.Web --urls http://localhost:5080
```

Применить миграции и идемпотентный seed без запуска HTTP-сервера:

```powershell
dotnet run --project src/Marketplace.Web -- --seed-only
```

## Проверка

```powershell
dotnet build Marketplace.slnx
./scripts/architecture-smoke.ps1
cd tests/UiTests
npm install
npx playwright install chromium
npm test
```

Контролируемая проверка `503` и восстановления readiness (временно останавливает только локальный PostgreSQL-контейнер, не удаляя volume):

```powershell
./scripts/readiness-failure-drill.ps1
```

Проверка сохранения выданного OTP после перезапуска только web-контейнера:

```powershell
./scripts/otp-restart-drill.ps1
```

Smoke-тест запускает сайт сам и проверяет 390, 768, 1366 и 1920 px. Скриншоты сохраняются в `tests/UiTests/test-results/screenshots`.

GitHub Actions workflow `.github/workflows/ci.yml` повторяет Release build, проверку отказа небезопасной Production-конфигурации, сборку hardened Docker stack, полный Playwright-набор и OTP/readiness failure drills. При ошибке сохраняются trace, screenshots, HTML report и container logs.

## Структура

- `src/Marketplace.Web` — Razor Pages приложение и модуль каталога;
- `src/Marketplace.Web/Data/sample-listings.json` — рабочая копия seed-данных;
- `src/Marketplace.Web/wwwroot/demo/images` — локальные demo-фотографии;
- `tests/UiTests` — Playwright smoke-тест;
- `docs/reference` — исходный пакет требований из архива без смысловых изменений;
- `docs/milestones` — checklist и ограничения завершённых этапов.

Точка продолжения, результаты приёмки и production backlog: [`docs/PROJECT_LOG.md`](docs/PROJECT_LOG.md).

Pre-release правовые страницы доступны по маршрутам `/Privacy`, `/Terms`, `/Cookies` и `/Rules`; до внешней беты их нужно заменить утверждёнными документами с реквизитами оператора.
