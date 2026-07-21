# Журнал разработки Marketplace

Этот файл — постоянная точка продолжения проекта. Его нужно обновлять после каждого законченного блока и перед сменой контекста.

## Текущий статус

- Активный этап: **post-M13 production readiness**.
- Текущая точка: delivery plan M0–M13 принят; завершён 54-й post-M13 блок — шапки главной и личного кабинета геометрически выровнены. Обязательных локальных пунктов текущего плана не осталось; следующие production-работы требуют инфраструктуры, провайдеров и внешней проверки.
- Локальный адрес: `http://localhost:5080`.
- Хранилище: PostgreSQL в Docker, порт хоста `5433`.

## Завершено

### M0 — Bootstrap и живой UI-shell

- ASP.NET Core Razor Pages solution, Docker и health-check.
- Каталог объявлений с демонстрационными данными и картой-заглушкой.
- Адаптивный интерфейс, светлая и тёмная темы, базовые Playwright-проверки.

### M1 — Дизайн-система и навигация

- Галерея компонентов `/Components`, интерактивные состояния и сохранение темы.
- Компактная sticky-шапка при прокрутке.
- Исправлена нестабильность шапки у нижней границы страницы: раздельные пороги и `requestAnimationFrame` в `wwwroot/js/site.js`.

### M2 — Identity и профиль

- EF Core + ASP.NET Identity + PostgreSQL (`MarketplaceDbContext`).
- Миграция `Modules/Identity/Migrations/*InitialIdentity*`.
- Вход и регистрация через одноразовый код локального fake email provider: `/Account/SignIn`.
- Профиль и приватность: `/Account/Profile`.
- Активные сессии и отзыв чужих сессий: `/Account/Sessions`.
- Роли `Member`, `Moderator`, `Administrator`, демо админа `admin@marketplace.local`, аудит действий: `/Account/RoleDemo`.
- Новые UI-тесты: `tests/UiTests/specs/identity.spec.js`.
- Приёмка 2026-07-17: Release build без ошибок/предупреждений; 13 Playwright-тестов; `GET /health` = `200 Healthy`; PostgreSQL healthy; migration table и все Identity-таблицы созданы.

### M5 — Модерация

- Сущности `ModerationCase`, `ModerationFinding`, `ModerationDecision`, `ModerationAppeal`; миграция `ModerationWorkflow` применена.
- Demo-rules с ruleset `demo-1.0`; Critical finding сразу переводит объявление в `Quarantine`.
- `/Admin/Moderation` — приоритетная очередь, `/Admin/ModerationReview` — карточка findings и решения; телефон владельца намеренно не загружается и показывается как скрытый.
- Владелец видит точную причину, проблемное поле, правило и инструкцию, может исправить и повторно отправить объявление.
- Апелляция переводит объявление и кейс в `Appealed`, возвращает его в начало очереди и сохраняется в audit trail.
- Приёмка 2026-07-17: Release build без ошибок/предупреждений; 18/18 Playwright-тестов; Docker и PostgreSQL healthy.

### M6 — Поиск и фильтры

- `DatabaseListingCatalog` выдаёт активные объявления PostgreSQL; без критериев используется стабильный JSON demo-fallback.
- Поиск объединяет русский PostgreSQL full-text, `ILIKE` и `pg_trgm` `word_similarity`; миграция `SearchTrigrams` применена.
- Общие фильтры: категория, цена, состояние, тип сделки, фото; категорийные поля строятся из опубликованной схемы атрибутов.
- Сортировки и фильтры представлены GET-параметрами, переживают refresh; сортировка сохраняется cookie на 365 дней.
- Приёмка 2026-07-17: Release build без ошибок/предупреждений; 19/19 Playwright-тестов; visual regression, Docker/PostgreSQL и `GET /health` прошли.

### M7 — География и карта

- `ListingLocation` хранит exact/private и public coordinates раздельно; публикация создаёт стабильную приблизительную точку.
- `IIpGeoAdapter` даёт fallback Москва, browser geolocation проходит через handler и не блокирует ручной выбор при отказе.
- `DemoYandexMapAdapter` изолирует внешний provider и строит кластеры; на zoom 18 выдаёт individual previews.
- Москва/5 городов/Россия, радиус Haversine, list/split/map mode и выбор пользователя сохраняются cookie.
- Приёмка 2026-07-17: Release build 0 ошибок/предупреждений; 22/22 Playwright-тестов; visual baseline проверен и обновлён; Docker/PostgreSQL healthy.

### M8 — Чат

- `Conversation`, `ChatMessage`, `ChatAttachment`, blocks/reports/templates/auto-replies; миграция `MessagingCore` применена.
- `/Messages` и `/Messages/Chat`; чат можно начать только по Active объявлению, existing history остаётся доступна.
- SignalR `/hubs/chat`: persistence-before-broadcast, reconnect, typing, presence, delivery/read receipts; локальный raw JSON protocol client без CDN.
- Вложения: максимум 3 × 20 МБ, whitelist JPG/PNG/WebP/PDF/TXT, хранение вне webroot и авторизованная выдача.
- Внешние ссылки блокируются; report/block и быстрые ответы/автоответ реализованы.
- Приёмка 2026-07-17: Release build 0/0; 23/23 Playwright-тестов с двумя browser contexts; health 200; Docker/PostgreSQL healthy.

### M9 — Избранное, сравнение, сделки и отзывы

- `Favorite`, `ComparisonItem`, `Deal`, `Review`, `UserNotification`; миграция `EngagementDealsReviews` применена.
- `/Favorites` хранит цену добавления и показывает price-drop alerts; `/Compare` сравнивает только одну категорию и dynamic attributes.
- В чате покупатель запрашивает Deal, продавец подтверждает, listing становится `Completed`; системные сообщения и уведомления создаются транзакционно.
- Review доступен только сторонам `Confirmed` deal, уникален по author/deal, rating ограничен 1–5.
- Приёмка 2026-07-17: Release build 0/0; 24/24 Playwright-тестов, M9 end-to-end зелёный.

### M10 — Рекомендации и сохранённые поиски

- Добавлены `ListingViewEvent`, `SearchHistoryEntry`, `SavedSearch`; миграция `RecommendationsAndSavedSearches` применена.
- Просмотры и поиски дедуплицируются, обновляют время последней активности и автоматически очищаются после 30 дней.
- Карточка объявления показывает rule-based похожие объявления и недавно просмотренные; ранжирование учитывает категорию, город, состояние, цену, фото и свежесть.
- `/Recommendations` управляет сохранёнными поисками, уведомлениями, историей и показывает новые совпадения относительно предыдущей проверки.
- Настройка профиля `UseHistoryForRecommendations` прекращает запись и показ персональной истории; сохранённые поиски работают независимо.
- Приёмка 2026-07-18: Release build 0/0; 25/25 Playwright-тестов; health 200; Docker/PostgreSQL healthy.

### M11 — Административная платформа

- Единый `/Admin` shell объединяет dashboard, пользователей/роли, объявления, существующие каталог/модерацию и операционный центр.
- Добавлены роли Owner, SeniorAdministrator, CatalogAdministrator, SeniorModerator, Support, FinanceOperator, FinanceController, SecuritySpecialist и Auditor; обычный пользователь получает явный 403.
- `FeatureFlag` и `BackgroundJobRun`; миграция `AdministrativePlatform` применена.
- Ручные demo-задачи выполняют retention истории и saved-search matching с сохранением результата.
- `AuditEvent` расширен ролью, объектом, old/new values, причиной и correlation id; изменения ролей, статусов, flags и запусков задач аудируются.
- Приёмка 2026-07-18: Release build 0/0; 26/26 Playwright; health 200; Docker/PostgreSQL healthy.

### M12 — Монетизация в sandbox

- `PlanDefinition`, `Entitlement`, `PromotionProduct`, `PromoCode`, `BonusLedgerEntry`, `PaymentTransaction`, `PromotionPurchase`, `RefundRequest`; миграция `SandboxMonetization` применена.
- Публикационная квота больше не зашита в handler и читается через `QuotaService`; расширенный тариф даёт лимит 50.
- Fake payment идемпотентен, разделяет деньги/скидку/бонусы; бонусы ведутся append-only ledger.
- Возврат проходит operator preparation и controller approval, создаёт reversal и финансовый аудит.
- Приёмка 2026-07-18: Release build 0/0; 27/27 Playwright на контрольной точке M12.

### M13 — Hardening и закрытая бета

- Security headers, rate limiter входа, PostgreSQL readiness, invite-only registration mode и явный 403.
- Accessibility smoke: skip-link/focus, lang, main landmark, alt/labels; существующие responsive/visual baselines сохранены.
- Load drills: 100/100 readiness за 1,46 с; 10 000 moderation evaluations существенно быстрее порога 2 с.
- Backup/restore drill восстановил 49 public tables в изолированную временную БД и удалил её.
- Runbooks находятся в `docs/operations`; резервные дампы исключены из git.
- Финальная приёмка 2026-07-18: Release build 0/0; 30/30 Playwright; health/readiness 200; Docker/PostgreSQL healthy.

### Post-M13 — Production readiness, блок 1

- `MaintenanceJobWorker` обрабатывает PostgreSQL-backed очередь, восстанавливает stale `Running`, делает до трёх попыток и автоматически планирует retention/saved-search jobs.
- Миграция `DurableMaintenanceQueue` применена; admin UI показывает статус и число попыток.
- Production configuration fail-fast отклоняет wildcard/localhost hosts, открытую регистрацию, слабый invite и demo DB credentials; smoke подтвердил ожидаемый отказ.
- Контейнер запускается как `app` (`uid=1654`), с read-only root, `cap_drop: ALL`, `no-new-privileges`, tmpfs `/tmp` и DB-aware healthcheck.
- Chat/uploads/data-protection вынесены в persistent volumes; после restart количество контрольных файлов сохранилось.
- Приёмка 2026-07-18: Release build 0/0; критический набор 6/6 и полный набор 30/30; контейнер healthy.

### Post-M13 — Observability foundation, блок 2

- Все HTTP-ответы содержат `X-Correlation-ID`; корректный входящий GUID сохраняется, неверный заменяется. Идентификатор используется как `TraceIdentifier` и scope структурированного лога, поэтому существующий audit trail получает то же значение.
- `RuntimeMetrics` собирает process-local HTTP counters/gauge/duration, классы статусов и ограниченные по cardinality результаты durable worker.
- `/metrics` выдаёт Prometheus text format; в Production требует отдельный `Observability__MetricsToken` длиной не менее 24 символов, который также проверяется fail-fast validator.
- Добавлен `docs/operations/OBSERVABILITY.md` с scrape contract, минимальными alert rules и границами встроенного решения.
- Приёмка 2026-07-18: Release build 0/0; observability/hardening 4/4; полный набор 31/31; readiness 200; контейнер healthy.

### Post-M13 — Multi-replica worker safety, блок 3

- Планирование recurring maintenance jobs сериализовано PostgreSQL advisory transaction lock, поэтому несколько реплик не создают одинаковые интервальные задания одновременно.
- Claim задания выполняется короткой транзакцией через `FOR UPDATE SKIP LOCKED`; статус `Running`, lock time и attempts сохраняются до освобождения строки.
- Длительная обработка идёт после commit claim-транзакции; stale-lock recovery и retry/backoff сохранены.
- Приёмка 2026-07-18: Release build 0/0; полный набор 31/31; Docker app/PostgreSQL healthy; runtime worker log без ошибок EF/SQL.

### Post-M13 — CSP script hardening, блок 4

- Ранний theme bootstrap вынесен из inline-блока в same-origin `wwwroot/js/theme-init.js` и по-прежнему выполняется до загрузки CSS.
- `script-src` и `style-src` ограничены `'self'` без `'unsafe-inline'`; динамические координаты demo-карты кодируются внешними CSS-классами с точностью 0,1%.
- Hardening smoke проверяет директиву CSP и отсутствие CSP violations в browser console.
- Приёмка 2026-07-18: Release build 0/0; security/visual набор 9/9; полный набор 31/31; visual baselines без изменений; контейнер healthy.

### Post-M13 — OTP and bootstrap-admin hardening, блок 5

- Глобальный auth limiter заменён раздельными fixed-window partitions по remote IP и действию: page 120/мин, code send 60/мин, verify 40/мин; отказ возвращает `429` и `Retry-After: 60`.
- Fake OTP дополнительно ограничен пятью неверными попытками на email/code; повторная выдача в течение минуты не генерирует новый код.
- Автоматический demo Administrator теперь определяется `Identity:DemoAdminEmail`, работает только в Development и скрыт из production UI. Production fail-fast отвергает непустую настройку.
- Добавлен последний в suite destructive smoke `zz-auth-rate-limit.spec.js`, подтверждающий достижение `429` без влияния на остальные identity-сценарии.
- Приёмка 2026-07-18: Release build 0/0; полный набор 32/32; контейнер healthy.

### Post-M13 — Reverse-proxy trust boundary, блок 6

- `UseForwardedHeaders` выполняется до correlation/rate limiting/auth и принимает только `X-Forwarded-For` + `X-Forwarded-Proto` с симметрией и `ForwardLimit = 1`.
- Доверие задаётся только точными IP из `ReverseProxy:KnownProxies`; при явной конфигурации framework loopback defaults очищаются.
- Production fail-fast требует минимум один корректный proxy IP. Для environment variables используется `ReverseProxy__KnownProxies__0`, `__1` и далее.
- Development probe и hardening smoke подтверждают, что прямой клиент не может подменить IP `203.0.113.42` или схему `https`.
- Приёмка 2026-07-18: Release build 0/0; hardening 5/5; полный набор 33/33; unsafe production defaults отклонены; контейнер healthy.

### Post-M13 — Reproducible CI, блок 7

- Добавлен `.github/workflows/ci.yml` для push в `main`/`codex/**`, pull request и ручного запуска; права workflow ограничены `contents: read`, конкурирующие запуски одной ветки отменяются.
- CI выполняет Release build, fail-fast Production smoke, сборку и запуск hardened Docker Compose stack, ожидание DB readiness и полный Playwright suite.
- При ошибке сохраняются HTML report, traces/screenshots и container logs на 7 дней; ephemeral volumes удаляются в always-step.
- `production-config-smoke.ps1` переведён на cross-platform path construction для Windows/Linux runners.
- Локальная приёмка 2026-07-18: workflow YAML parsed, `docker compose config --quiet`, Release build 0/0 и Production smoke успешно выполнены. Удалённый GitHub run возможен после публикации репозитория.

### Post-M13 — Authentication telemetry, блок 8

- `RuntimeMetrics` публикует `marketplace_auth_events_total` только с bounded labels `action=page|send|verify|other` и `outcome=success|failed|rate_limited|other`.
- OTP send/verify записывают outcome; `OnRejected` rate limiter записывает действие до возврата `429`.
- Неуспешная проверка создаёт structured warning с remote IP и общим correlation scope; email и OTP никогда не попадают в metric labels или log message.
- `zz-auth-rate-limit.spec.js` подтверждает наличие `marketplace_auth_events_total{action="verify",outcome="rate_limited"}`.
- Приёмка 2026-07-18: Release build 0/0; полный набор 33/33; контейнер healthy.

### Post-M13 — Idempotent deployment seed command, блок 9

- Аргумент `--seed-only` строит DI container, применяет EF migrations и весь идемпотентный `IdentitySeed`, пишет completion log и завершается до HTTP pipeline/hosted worker.
- Режим не обходит Production fail-fast: опубликованная DLL с небезопасными production defaults ожидаемо отклоняется.
- README содержит локальную команду; CI выполняет тот же режим внутри hardened container после readiness.
- Приёмка 2026-07-18: два последовательных Development-запуска вернули 0, число public tables осталось 49; container exec также завершился 0 с `No migrations were applied`; web/PostgreSQL healthy.

### Post-M13 — HTTP transport and upload budgets, блок 10

- Kestrel ограничен абсолютным body ceiling 105 MiB, 15-секундным header timeout, 100 headers/32 KiB, request line 8 KiB и keep-alive 2 минуты.
- До routing handlers middleware применяет 1 MiB по умолчанию, 65 MiB к `/Messages/Chat` и 105 MiB к `/Listings/Create`, возвращая `413` до чтения/разбора формы.
- `FormOptions` ограничивает multipart ceiling, количество значений 2048, key 2 KiB и отдельное value 64 KiB; существующие handler-ограничения файлов (чат 3×20 MiB, объявления до 10×10 MiB) сохранены.
- Development probe возвращает route budget; smoke передаёт реальный 2 MiB payload и подтверждает `413`, затем проверяет 65/105 MiB budgets без тяжёлого трафика.
- Приёмка 2026-07-18: Release build 0/0; hardening 6/6; полный набор 34/34; Docker app/PostgreSQL healthy.

### Post-M13 — Selective response compression, блок 11

- Fingerprinted static assets уже обслуживались `MapStaticAssets` с precompressed Brotli и `Cache-Control: max-age=31536000, immutable`; этот готовый контур сохранён без дублирования middleware.
- Динамический Brotli/Gzip включён только для публичных GET путей `/`, `/Catalog`, `/Components`, `/health`, `/metrics`; HTTPS поддерживается за доверенным proxy.
- Identity/account/messages/admin/listing-management и другие персональные страницы намеренно исключены, чтобы не расширять BREACH-поверхность.
- Smoke подтверждает `Content-Encoding: br` + `Vary: Accept-Encoding` на главной и отсутствие compression на `/Account/SignIn`.
- Приёмка 2026-07-18: главная уменьшена с 32 911 до 8 078 байт (−75,5%); Release build 0/0; hardening 7/7; полный набор 35/35; контейнер healthy.

### Post-M13 — Architecture invariants, блок 12

- Cross-platform `scripts/architecture-smoke.ps1` проверяет порядок `ForwardedHeaders → RateLimiter → Authentication → Authorization` и наличие ключевых Production fail-fast guards.
- Каждый Admin PageModel обязан иметь явный role-based `[Authorize]`; Razor запрещает inline/remote scripts/styles и `Html.Raw`.
- Собственный C# код (кроме generated migrations/bin/obj) проверяется на sync-over-async `.Result/.Wait()` и hardcoded `admin@marketplace.local`.
- Workflow выполняет architecture smoke сразу после Release build, до дорогостоящих Docker/Playwright шагов; README содержит локальную команду.
- Приёмка 2026-07-18: architecture smoke passed; Release build 0/0; Compose config valid; приложение после предыдущего полного набора 35/35 остаётся healthy.

### Post-M13 — OTP provider boundary, блок 13

- Страница входа зависит от provider-neutral `IOneTimeCodeService`; локальная реализация остаётся `FakeEmailCodeService`, выбираемая явным `Identity:OtpProvider=Fake`.
- Debug-код и fake-пояснения отдаются UI только в Development; verify form не зависит от наличия debug-кода и готов к асинхронному внешнему provider.
- Production fail-fast безусловно отклоняет Fake/отсутствующий provider. Неизвестное имя provider также завершает startup ошибкой вместо скрытого fallback.
- Architecture smoke защищает наличие guard `Fake OTP provider must be replaced in Production`; deployment должен передать реальный зарегистрированный adapter через `Identity__OtpProvider`.
- Приёмка 2026-07-18: Release build 0/0; architecture/Production smoke green; Identity+hardening 9/9; полный набор 35/35; контейнер healthy.

### Post-M13 — Database outage and graceful worker resilience, блок 14

- EF database commands ограничены 15 секундами, host graceful shutdown — 15 секундами; readiness registration имеет 3-секундный budget.
- `DatabaseReadinessHealthCheck` больше не использует потенциально долгий `DbContext.CanConnectAsync`: отдельное Npgsql соединение задаёт connect/command timeout 2 секунды, linked deadline 2,5 секунды и выполняет `SELECT 1`.
- Cancellation после durable job claim теперь пытается вернуть запись в `Queued` отдельным 3-секундным shutdown token; при неудаче логируется ошибка и остаётся проверенный stale-lock recovery.
- `scripts/readiness-failure-drill.ps1` безопасно останавливает только PostgreSQL service, требует `503` не позднее 6 секунд и в `finally` запускает БД с ожиданием восстановления обоих containers; volumes не удаляются.
- CI выполняет drill после Playwright. Ручная приёмка 2026-07-18: первый drill обнаружил зависание >6 с и привёл к исправлению; повторный вернул `503` за 4,01 с, автоматизированный — за 0,02 с на уже разорванном pool; recovery healthy; после outage полный набор 35/35.

### Post-M13 — Upload content signature validation, блок 15

- Единый потоковый `UploadContentValidator` читает не более первых 4 KiB и сверяет declared MIME с magic bytes JPEG, PNG, RIFF/WebP и PDF.
- `text/plain` требует непустой strict UTF-8 без NUL и запрещённых control characters. Неизвестные MIME отклоняются.
- Chat и listing photo uploads используют inspector после size/count/extension/MIME metadata checks и до создания файла; storage keys остаются server-generated GUID.
- Messaging E2E теперь доказывает три ветки: oversized PDF отклонён, HTML переименованный в PDF отклонён, безопасный UTF-8 TXT сохранён и доступен участнику. Realtime/history assertions сохранены.
- Приёмка 2026-07-18: Release build 0/0; architecture smoke green; messaging 1/1; полный набор 35/35; контейнер healthy.

### Post-M13 — Atomic upload persistence, блок 16

- `AtomicUploadStorage` пишет upload в уникальный hidden `.uploading` файл, flush с `WriteThrough`, затем атомарно переименовывает в server-generated GUID filename; частичный temp/final удаляется при исключении.
- Chat оборачивает `MessagingService.SendAsync`, attachment metadata и commit в одну PostgreSQL transaction. При business/I/O/DB ошибке созданные файлы компенсирующе удаляются, поэтому не остаётся сообщения без metadata или orphan-файла.
- Listing details также использует DB transaction и compensating cleanup для photo files при неуспешном Save/commit.
- Architecture smoke требует `AtomicUploadStorage.SaveAsync` в обоих handlers и запрещает возврат прямого `File.Create`.
- Приёмка 2026-07-18: Release build 0/0; architecture smoke green; listing+messaging 2/2; полный набор 35/35; контейнер healthy.

### Post-M13 — Interrupted upload temp lifecycle, блок 17

- `UploadTemporaryFileCleanup` рекурсивно обходит только разрешённые chat/listing upload roots, пропускает reparse points, нормализует full path и удаляет исключительно `.*.uploading` старше заданного cutoff.
- Worker автоматически ставит `upload-temp-cleanup` не чаще раза в час; job участвует в общей PostgreSQL durable queue, retry/backoff/stale-lock и Prometheus telemetry.
- Administrator может поставить cleanup вручную из Operations; запуск аудируется существующим `admin.job.queued` flow. Auditor остаётся read-only.
- Development smoke создаёт stale temp, fresh temp и completed file: удаляется ровно stale, остальные сохраняются, probe directory затем удаляется.
- Приёмка 2026-07-18: Release build 0/0; architecture smoke green; administration+hardening 9/9; полный набор 36/36; контейнер healthy.

### Post-M13 — Durable multi-replica OTP challenges, блок 18

- Fake OTP challenge state перенесён из process-local `ConcurrentDictionary` в PostgreSQL; миграция `DurableOtpChallenges` создаёт уникальную запись на нормализованный email и индексы email/expiry.
- В БД хранится salted SHA-256 hash кода, 16-byte salt, expiry, resend cutoff и attempts. Debug code существует только для Development fake-provider; Production по-прежнему fail-fast запрещает Fake.
- `IOneTimeCodeService` полностью асинхронный и scoped. Issue/verify сериализованы keyed PostgreSQL advisory transaction lock и `FOR UPDATE`, поэтому restart и несколько реплик не теряют challenge и не расходятся по attempts/resend.
- Hourly durable job `otp-challenge-cleanup` удаляет просроченные записи; Administrator может запустить его вручную из Operations. Проверено фактическое завершение job в PostgreSQL.
- Добавлен `scripts/otp-restart-drill.ps1`: код запрашивается через реальную Razor/antiforgery форму, web container перезапускается, тот же код и cookie завершают вход после readiness recovery.
- Попутно устранены UTC-offset defect в moderation daily counter и неявное доверие forwarded headers при пустом proxy allowlist; multi-user Playwright helper изолирует antiforgery cookies.
- Приёмка 2026-07-18: Release build 0/0; architecture и Production smoke green; restart drill green; targeted 9/9; полный набор 36/36; Docker app/PostgreSQL healthy.

### Post-M13 — Independent liveness and database readiness, блок 19

- Добавлен tagged self health check и независимый `/health/live`, который подтверждает работу процесса без обращения к PostgreSQL.
- `/health/ready` по-прежнему выполняет короткую DB-проверку и предназначен для снятия трафика; совместимый `/health` выполняет все проверки. Compose healthcheck остаётся readiness-based.
- `readiness-failure-drill.ps1` теперь при остановленной БД одновременно требует liveness `200` не медленнее 2 секунд и readiness `503` не медленнее 6 секунд, затем проверяет полное восстановление обоих контейнеров.
- README и deployment readiness описывают назначение трёх endpoint; hardening browser test защищает наличие отдельного live endpoint. CI автоматически получает расширенный drill через существующий шаг.
- Приёмка 2026-07-18: liveness `200` за 0,01 с и readiness `503` за 4,01 с при остановленном PostgreSQL; recovery healthy; Release build 0/0; architecture smoke green; hardening 8/8; полный набор после outage 36/36.

### Post-M13 — Distributed authentication rate limiting, блок 20

- Добавлена миграция `DistributedAuthRateLimits` и PostgreSQL-backed fixed-minute buckets для sign-in page, OTP send и OTP verify с прежними лимитами 120/60/40.
- Bucket key — только SHA-256 от remote IP, action и UTC window; исходный IP не сохраняется. Атомарный `INSERT … ON CONFLICT DO UPDATE … RETURNING` создаёт общий счётчик для всех реплик.
- Distributed middleware выполняется после trusted forwarded-header boundary и до локального ASP.NET limiter. Локальный limiter сохранён как дешёвый второй рубеж; общий PostgreSQL контур возвращает совместимые `429` и `Retry-After: 60`, пишет существующую bounded auth telemetry и fail-closed отвечает `503`, если throttle storage недоступен.
- Durable job `auth-rate-limit-cleanup` ежечасно удаляет expired buckets; доступен ручной Administrator action. В PostgreSQL подтверждены 64-символьные ключи, verify count 41 и завершённый cleanup run.
- Architecture smoke теперь фиксирует порядок `ForwardedHeaders → DistributedAuthRateLimit → RateLimiter → Authentication → Authorization`.
- Приёмка 2026-07-18: Release build 0/0; architecture smoke green; identity/rate-limit 3/3; полный набор 36/36; Docker app/PostgreSQL healthy.

### Post-M13 — Cookie and absolute session lifecycle, блок 21

- Application cookie теперь имеет явные HttpOnly, essential, path `/`, SameSite Lax и environment-aware Secure; в Production используется host-only имя `__Host-marketplace.session` и `Secure=Always`.
- Antiforgery cookie получил отдельную явную политику: HttpOnly, essential, path `/`, SameSite Strict, `__Host-marketplace.csrf` и Secure в Production; Development сохраняет HTTP-совместимые имена.
- Sliding cookie больше не продлевает серверную сессию бесконечно: `UserSession.CreatedAt` задаёт абсолютный максимум 30 дней. Истёкшая/отозванная/повреждённая principal отклоняется, а application cookie удаляется через `SignOutAsync`.
- Daily durable job `session-retention` удаляет серверные session rows старше абсолютного срока и старые revoked records; доступен ручной Administrator action. Завершённый run подтверждён в PostgreSQL.
- Identity browser test проверяет фактические Development flags session/CSRF cookies; architecture smoke защищает Production `__Host-`, Secure и Strict invariants.
- Приёмка 2026-07-18: Release build 0/0; architecture и Production smoke green; identity+administration 3/3; полный набор 36/36; контейнеры healthy.

### Post-M13 — Private response cache and cross-origin isolation, блок 22

- Централизованная response policy выставляет `Cache-Control: no-store, no-cache`, `Pragma: no-cache` и `Expires: 0` для любого авторизованного ответа и чувствительных анонимных путей Account/Messages/Admin/Favorites/Compare/Recommendations/Monetization/listing management.
- Публичные анонимные страницы не получают `no-store`, поэтому selective compression и допустимое edge/browser caching не отключены глобально; авторизованный public route всё равно защищён по principal.
- Ко всем ответам добавлены `X-Frame-Options: DENY` как defense-in-depth к CSP `frame-ancestors 'none'`, `Cross-Origin-Opener-Policy: same-origin` и `Cross-Origin-Resource-Policy: same-origin`.
- Hardening test разделяет public и sensitive cache semantics и проверяет новые security headers; Identity test подтверждает `no-store` на реальном авторизованном Profile response. Architecture smoke фиксирует policy tokens.
- Приёмка 2026-07-18: реальные curl headers подтверждены; Release build 0/0; architecture и Production smoke green; hardening+identity 10/10; полный набор 36/36; контейнеры healthy.

### Post-M13 — Strict style CSP and map positioning, блок 23

- Последнее разрешение `style-src 'unsafe-inline'` удалено из CSP: скрипты и стили теперь загружаются только с текущего origin, без inline-исключений.
- Серверные координаты маркеров и кластеров карты преобразуются в ограниченный набор внешних CSS-классов с шагом 0,1%; кнопки, ссылки, hover-связь с карточками и адаптивная карта сохранили прежнюю семантику.
- Architecture smoke запрещает `style=` во всех Razor views и защищает строгий `style-src`; browser hardening test проверяет заголовок и отсутствие CSP-ошибок в консоли.
- Приёмка 2026-07-18: Release build 0 ошибок/0 предупреждений; architecture smoke green; catalog/design-system/hardening 19/19, включая визуальные baseline и стабильность шапки у низа страницы; полный набор 36/36; Docker app/PostgreSQL healthy.

### Post-M13 — Dependency vulnerability gate, блок 24

- Добавлен cross-platform `scripts/dependency-audit.ps1`: он запрашивает машинно-читаемый NuGet audit для прямых и транзитивных пакетов и npm audit для Playwright toolchain.
- Любая известная уязвимость или ошибка обращения к audit registry завершает проверку с ошибкой; CI выполняет gate сразу после Release build, до запуска Docker и браузерного набора.
- Architecture smoke защищает наличие dependency gate в workflow. Текущий локальный аудит подтверждает 0 известных NuGet и 0 npm vulnerabilities.
- Приёмка 2026-07-18: dependency audit green; architecture smoke green; предыдущий Release build 0/0 и полный Playwright набор 36/36 остаются актуальны; Docker app/PostgreSQL healthy.

### Post-M13 — Pre-release legal surfaces, блок 25

- Стандартная англоязычная заглушка `/Privacy` заменена русским уведомлением, соответствующим фактическому локальному контуру; добавлены `/Terms`, `/Cookies` и `/Rules`.
- Документы описывают данные и пользовательский контроль, технические cookies/localStorage, сессии, ограничения demo-платежей, модерацию и запрещённые товары. Каждый документ явно помечен как pre-release и не выдаётся за утверждённую юридическую редакцию.
- Общий адаптивный footer ведёт ко всем документам со всех страниц. Architecture smoke защищает наличие страниц, ссылок и отсутствие стандартной Privacy-заглушки; новый browser test проверяет маршруты, навигацию и отсутствие горизонтального overflow на 390 px.
- Осознанное изменение footer визуально проверено, baseline светлой/тёмной темы обновлены. Приёмка 2026-07-19: Release build 0/0; architecture smoke green; targeted 14/14; полный Playwright набор 37/37; Docker app/PostgreSQL healthy.

### Post-M13 — SignalR disconnect lifecycle, блок 26

- Аудит после полной регрессии обнаружил framework `fail` от `OperationCanceledException`: браузер закрывал WebSocket, пока `JoinConversation` сохранял read receipt в PostgreSQL.
- `JoinConversation`, `SendMessage` и `MarkRead` теперь поглощают исключительно ожидаемую отмену, когда `Context.ConnectionAborted` действительно сработал. Ошибки доступа, валидации и БД не маскируются.
- Нулевые записи process-local presence counter удаляются атомарным compare-by-value, поэтому множество отключившихся пользователей не создаёт неограниченный словарь; reconnect race не может удалить уже увеличенный счётчик.
- Architecture smoke защищает cancellation filter и presence cleanup. Приёмка 2026-07-19: Release build 0/0; realtime/identity/engagement 4/4; полный Playwright набор 37/37; после полного набора в свежих логах нет `fail`, unhandled exceptions или cancellation noise; Docker app/PostgreSQL healthy.

### Post-M13 — Safe localized error surface, блок 27

- Стандартная англоязычная ASP.NET Error-заглушка и инструкции про Development Mode удалены. Пользователь получает нейтральную русскую страницу с действиями возврата и повтора без exception details.
- Error endpoint явно возвращает HTTP `500`, остаётся `no-store` и показывает только UUID correlation ID, совпадающий с `X-Correlation-ID`; внутренний `Activity`/stack trace не раскрывается.
- Architecture smoke запрещает диагностические шаблоны на Error page и защищает 500/correlation invariants. Hardening browser test проверяет status, cache policy, заголовок, тело и отсутствие Development/StackTrace markers.
- Приёмка 2026-07-19: Release build 0/0; architecture smoke green; hardening+legal 10/10; полный Playwright набор 38/38; Docker app/PostgreSQL healthy.

### Post-M13 — Self-service personal data export, блок 28

- Авторизованная страница `/Account/Data` формирует скачиваемый versioned JSON с профилем/настройками/ролями, сессиями и аудитом, объявлениями и точками владельца, чатами, engagement/recommendation history и sandbox-монетизацией.
- Экспорт использует только `AsNoTracking` projections и не включает password/OTP hashes, security stamps, normalized identity fields, внутренние attachment storage keys или профильные данные собеседников. Метаданные вложений сохраняются без пути хранения.
- Ответ отдаётся attachment с `application/json`, наследует private `no-store`; ссылка доступна в профиле и общем account navigation. Privacy notice теперь прямо указывает self-service копию данных.
- Architecture smoke защищает авторизацию, read-only projections, JSON attachment contract и запрет секретных полей. Identity browser test проверяет профиль, роли, активную сессию, cache/disposition headers и отсутствие secret field names.
- Приёмка 2026-07-19: Release build 0/0; architecture smoke green; identity 2/2; полный Playwright набор 38/38; после регрессии контейнеры healthy.

### Post-M13 — Durable account erasure requests, блок 29

- Миграция `AccountErasureWorkflow` добавляет durable очередь запросов с lifecycle `Pending → Cancelled/Approved/Rejected`; partial unique index PostgreSQL гарантирует не более одного открытого запроса пользователя при нескольких репликах.
- Пользователь создаёт подтверждённый запрос и может отменить его на `/Account/Data`. Оба действия фиксируются в audit trail; повторная конкурентная отправка идемпотентно использует существующий pending request.
- `/Admin/Privacy` доступен только Administrator/SeniorAdministrator/Owner/SecuritySpecialist: оператор видит очередь, обязан указать основание и подтверждает либо отклоняет запрос с отдельным audit event.
- Workflow намеренно не выполняет физическое удаление: retention/legal hold, анонимизация общих диалогов и финансовые сроки должны быть утверждены оператором. Architecture smoke запрещает обход процесса автоматическим delete из пользовательского handler и защищает DB uniqueness guard.
- Приёмка 2026-07-19: Release build 0/0; migration применена; architecture smoke green; administration+identity 3/3; полный Playwright набор 38/38; Docker app/PostgreSQL healthy.

### Post-M13 — Distributed personal export throttling, блок 30

- Тяжёлый `/Account/Data?handler=Export` ограничен пятью выгрузками на пользователя за 15 минут; шестой запрос получает `429` и точный `Retry-After`. Обычная страница Data и POST request/cancel erasure не затрагиваются.
- Новый middleware выполняется после Authentication, поэтому partition строится по user ID, но в PostgreSQL хранится только SHA-256 bucket key. Лимит общий для всех реплик; storage outage fail-closed возвращает `503`/`Retry-After: 5` до выполнения export queries.
- Auth limiter и privacy limiter переведены на общий `DistributedRateLimitStore` с прежним атомарным `INSERT … ON CONFLICT … RETURNING`; существующий hourly cleanup продолжает удалять expired buckets.
- Architecture smoke защищает middleware order, лимит/window, `429` и fail-closed `503`. Identity browser test подтверждает пять успешных экспортов, шестой отказ и то, что erasure workflow после rate limit остаётся доступен.
- Приёмка 2026-07-19: Release build 0/0; architecture smoke green; identity+OTP limiter 3/3; повторный изолированный полный Playwright набор 38/38; Docker app/PostgreSQL healthy.

### Post-M13 — Atomic erasure state transitions, блок 31

- Пользовательская отмена и административное Approved/Rejected больше не используют read-then-write tracked entity. Conditional `ExecuteUpdate` меняет только строку со статусом `Pending`, поэтому при гонке cancel/resolve или двух операторов побеждает ровно один запрос, остальные получают `409`.
- Status transition и соответствующий audit event находятся в одной PostgreSQL transaction: невозможны ни решение без аудита, ни аудит без изменения состояния.
- Перезапись уже Cancelled/Approved/Rejected состояния исключена; обязательная причина и RBAC оператора сохранены. Architecture smoke защищает transaction/conditional-update/commit invariants на обеих сторонах workflow.
- Administration и Identity tests отправляют реальные конкурентные POST и требуют ровно один `200` после redirect и один `409`, после чего проверяют финальное состояние и аудит.
- Приёмка 2026-07-19: Release build 0/0; architecture smoke green; race-targeted administration+identity 3/3; полный Playwright набор 38/38; Docker app/PostgreSQL healthy.

### Post-M13 — Secret-backed initial Owner provisioning, блок 32

- Добавлена отдельная deployment-команда `dotnet Marketplace.Web.dll --bootstrap-owner`; она не создаёт HTTP endpoint и читает email, отображаемое имя и capability только из `Bootstrap__OwnerEmail`, `Bootstrap__OwnerDisplayName`, `Bootstrap__OwnerToken`/подключённого configuration secret provider.
- Capability должен содержать минимум 32 символа и не передаётся через argv. Настройки команды валидируются до migration/seed и любых изменений БД. Обычный Production web startup fail-fast отклоняет оставленный bootstrap-токен, поэтому секрет удаляется сразу после успешного provisioning.
- PostgreSQL advisory transaction lock сериализует конкурентные запуски. Команда идемпотентна для того же первого Owner, запрещает создание другого Owner и атомарно сохраняет роли Member/Owner вместе с `identity.owner.bootstrapped` audit event.
- `bootstrap-owner-smoke.ps1` проверяет создание, повторный запуск, единственность роли и аудита, отказ второму Owner и в `finally` удаляет только собственные smoke-записи. Проверка включена в CI после запуска hardened stack; architecture smoke защищает entry point, secret guard, advisory lock и аудит.
- Приёмка 2026-07-19: Release build 0/0; architecture/dependency/unsafe-production-config smoke green; bootstrap create/idempotency/audit/second-Owner smoke green; полный Playwright набор 38/38; обновлённые Docker app/PostgreSQL healthy, live/ready 200, свежие логи чистые.

### Post-M13 — Homepage product navigation integration, блок 33

- Статические category buttons главной заменены ссылками на семь актуальных корневых категорий PostgreSQL и полный `/Catalog`; внутренние ссылки на Components удалены из публичной шапки и footer.
- «Избранное», «Сообщения» и «Подать объявление» ведут в существующие продуктовые разделы. Для авторизованного пользователя шапка показывает реальные количества избранных объявлений и непрочитанных сообщений вместо захардкоженного `2`; для гостя защищённые действия ведут на вход с локальным `ReturnUrl`.
- SignIn сохраняет `ReturnUrl` через выдачу/проверку OTP и после успешного входа использует только проверенный `Url.IsLocalUrl` + `LocalRedirect`. Сквозной browser test подтверждает маршрут `главная → Подать объявление → OTP → /Listings/Create`.
- Декоративные быстрые кнопки заменены рабочими GET-фильтрами «Новые», «С фото», «Бесплатно», «Сначала свежие». Для DB-backed карточек добавлен серверный toggle избранного с возвратом на исходный локальный URL; demo-карточки больше не содержат ложного локального favorite toggle.
- Architecture smoke защищает публичные product links, отсутствие internal Components entry и безопасный post-sign-in return. Светлый visual baseline обновлён после осознанного изменения шапки/категорий.
- Приёмка 2026-07-19: Release build 0/0; architecture/dependency smoke green; catalog integration 7/7; полный Playwright набор 39/39; визуальная проверка во встроенном браузере; Docker app/PostgreSQL healthy, live/ready 200, свежие логи чистые.

### Post-M13 — Hierarchical homepage category filtering, блок 34

- Верхние category chips больше не открывают `/Catalog` с деревом/формами структуры: они остаются на выдаче объявлений и передают `?category=<root-slug>`. Ссылка «Ещё» по-прежнему открывает полный справочник категорий намеренно.
- `DatabaseListingCatalog` раскрывает выбранный корневой раздел во все дочерние уровни и фильтрует объявления по множеству category IDs; «Транспорт» включает, например, седаны и кроссоверы.
- При пустом результате DB search больше не подменяется несвязанными JSON demo-карточками. Выбранный chip остаётся активным, заголовок показывает `Объявления: <категория>`, пустое состояние явно называет раздел.
- Architecture smoke защищает route-category и descendant search invariants. Browser verification подтвердил `/?category=transport`, активный «Транспорт» и специализированное пустое состояние.
- Приёмка 2026-07-19: Release build 0/0; architecture smoke green; catalog+search targeted 8/8; полный Playwright набор 39/39; Docker app/PostgreSQL healthy, live/ready 200, свежие логи чистые.

### Post-M13 — DB-backed development featured listings, блок 35

- Восемь прежних JSON-карточек перенесены в PostgreSQL как полноценные development-only `Listing` с детерминированными GUID, двумя demo-продавцами, реальными leaf-категориями/schema version, `ListingMedia`, `ListingLocation` и status history.
- Seed идемпотентен и вызывается только при `IWebHostEnvironment.IsDevelopment()`: Production не создаёт demo-пользователей или объявления. JSON-каталог оставлен только аварийным development fallback для пустой БД.
- Default development feed ограничен стабильным набором featured GUID, поэтому накопленные данные UI-тестов не раздувают главную до 100 карточек. Категории и поиск используют обычную полную DB-выдачу.
- Заголовки всех featured-карточек теперь ведут на `/Listings/Details?id=<guid>`; detail page загружает обычное объявление и предоставляет существующие Favorite/Compare/StartChat сценарии. Гостевой favorite ведёт через локальный SignIn return.
- Architecture smoke защищает Development-only registration и обязательные Listing/location/schema invariants. Добавлен browser test `featured cards open real listing details`; visual baselines светлой и тёмной главной осознанно обновлены.
- Приёмка 2026-07-19: Release build 0/0; architecture/dependency smoke green; catalog+engagement targeted 9/9; полный Playwright набор 40/40; встроенный браузер подтвердил переход BMW card → real detail + contact action; Docker app/PostgreSQL healthy, live/ready 200, свежие логи чистые.

### Post-M13 — Whole-card navigation and hover gallery, блок 36

- Заголовочная ссылка карточки растянута на всю её площадь: клик по изображению, описанию, цене или нижней части открывает реальный `/Listings/Details`; кнопка избранного остаётся отдельным интерактивным слоем. Лишний `tabindex` карточки удалён, клавиатурный focus остаётся на настоящей ссылке.
- BMW seed расширен до трёх `ListingMedia`: исходный передний ракурс, сгенерированный задний ракурс и профиль. Seed идемпотентно добавляет недостающие изображения и к уже существующему development-объявлению, поэтому очистка PostgreSQL не требуется.
- Главная загружает упорядоченную галерею из PostgreSQL. Горизонтальное положение мыши над фото выбирает кадр, уход с карточки возвращает первый; touch не перехватывается.
- Внизу изображения показано точное количество серых точек. Активный индикатор увеличивается, становится светлее и приподнимается; изображение, data-index и точка переключаются синхронно.
- Дополнительные растровые ассеты созданы встроенным ImageGen как точное редактирование `bmw.png` и сохранены в `wwwroot/demo/images/bmw-rear.png` и `bmw-side.png`. Финальные задания: тот же глянцевый чёрный седан на той же современной жилой улице в мягком пасмурном свете, формат 4:3 — отдельно задний левый ракурс и чистый профиль со стороны водителя; без людей, текста, водяных знаков и читаемого номера.
- Добавлен Playwright regression `the whole card opens details and BMW photos scrub under the pointer`, проверяющий три изображения/точки, зоны 52%/90%, возврат к первому кадру и клик по нижней области карточки.
- Приёмка 2026-07-19: Release build 0 ошибок/0 предупреждений; targeted 2/2; полный Playwright набор 41/41; встроенный браузер подтвердил кадры 1 → 2 → 3, синхронные точки и whole-card переход; Docker app/PostgreSQL healthy, `/health` 200.

### Post-M13 — Listing detail gallery and enlarged viewer, блок 37

- Страница `/Listings/Details` больше не выводит все фотографии одновременно: основной блок показывает ровно один активный кадр с `object-fit: contain`, стрелками, счётчиком и точками прямого выбора.
- Клик по фотографии открывает нативный modal `dialog` почти на весь экран. Увеличенный просмотр синхронно начинается с текущего кадра и поддерживает собственные стрелки, точки, циклическое листание, клавиши ←/→, Escape, крестик и клик по затемнённому фону.
- После визуальной проверки обычная desktop-галерея ограничена высотой 480 px: она целиком помещается рядом с информацией объявления, а увеличенный режим действительно показывает фотографию заметно крупнее. На мобильном основная сцена сохраняет 4:3, viewer занимает `100vw × 100dvh` без горизонтального переполнения.
- Состояния active/`aria-hidden`/`aria-current`, счётчики основной и увеличенной галерей обновляются вместе; объявление без фотографий сохраняет прежнее понятное пустое состояние.
- Добавлен Playwright regression `listing details show one photo and an enlarged navigable viewer`: три кадра, основной next, открытие на текущем кадре, viewer next, keyboard wrap, Escape и mobile full-screen/no-overflow.
- Приёмка 2026-07-19: targeted 2/2; полный Playwright набор 42/42; встроенный браузер подтвердил один основной кадр, компактную высоту 480 px и увеличенный просмотр; Docker app/PostgreSQL healthy.

### Post-M13 — Messaging interface polish, блок 38

- Список `/Messages` получил оформленные dialog cards: thumbnail, заголовок, собеседник, последняя реплика, дата/unread badge и отдельная круглая стрелка входа в стиле стрелок галереи. Hover поднимает карточку и подсвечивает стрелку.
- В `/Messages/Chat` текстовая ссылка `← Диалоги` заменена круглой кнопкой назад. Название объявления в `h1` теперь является прямой ссылкой на `/Listings/Details?id=...`; отдельная малозаметная ссылка «объявление» удалена.
- Переработаны стандартные browser controls: composer, кнопка отправки, file selector, подпись вложения, template/auto-reply/report inputs и textarea, focus rings, кастомный toggle автоответа, полноширинные sidebar actions и оформленные ссылки-вложения.
- Presence indicator теперь серый для `не в сети` и становится зелёным только после SignalR `PresenceChanged`; состояние обновляется вместе с текстом.
- Длинная account navigation больше не растягивает документ: она прокручивается внутри шапки без горизонтального overflow страницы. Mobile layout оставляет круглую кнопку назад, кликабельный заголовок и одноколоночные формы.
- Messaging regression расширен проверкой detail href заголовка, обеих круглых стрелок и mobile no-overflow; существующий сквозной чат, attachments, realtime, report и block сохранён.
- Приёмка 2026-07-19: targeted messaging 1/1; полный Playwright набор 42/42; встроенный браузер подтвердил CSS controls, circle radius 50%, detail route BMW и отсутствие overflow; Docker app/PostgreSQL healthy.

### Post-M13 — Unified chat attachments and public seller profile, блок 39

- Отдельная форма вложения удалена: file picker встроен круглой кнопкой-скрепкой в основной composer, подписи к файлу и кнопки «Прикрепить» больше нет. Текст и до трёх файлов отправляются одной кнопкой «Отправить» как одно сообщение.
- `OnPostSend` выполняет прежнюю строгую проверку размера/MIME/signature, создаёт attachment-message и сохраняет бинарные файлы/метаданные в существующей транзакции с компенсационной очисткой. Старый Upload handler оставлен совместимым, но больше не представлен в UI.
- Ctrl+Enter (и Cmd+Enter) вызывает отправку composer. При выбранных файлах JavaScript не перехватывает submit через SignalR, поэтому multipart проходит серверную валидацию; без файлов быстрые сообщения сохраняют realtime SignalR path и HTTP fallback.
- JPEG/PNG/WebP отображаются внутри bubble как фотографии, а не download links. Нажатие открывает modal viewer с contain-масштабированием, циклическими стрелками, клавишами ←/→, Escape и счётчиком. PDF/TXT остаются оформленными вложениями для скачивания.
- В правом блоке чата над verified/presence добавлено имя продавца-ссылка. Создана публичная `/Sellers/Details?id=...` с именем, городом, датой регистрации, подтверждением личности, рейтингом/отзывами и активными объявлениями продавца.
- Account navigation links закреплены `flex: 0 0 auto`: внутренний горизонтальный scroll больше не сжимает и не накладывает подписи друг на друга.
- Messaging e2e расширен: Ctrl+Enter, единый file input, oversized/signature rejection, TXT attachment, inline PNG + viewer/Escape, seller href и открытие публичного seller profile.
- Приёмка 2026-07-19: targeted messaging 1/1; полный Playwright набор 42/42; встроенный браузер подтвердил отсутствие отдельной attachment-form, inline image, единый composer, seller route с 4 активными объявлениями и no-overflow; Docker app/PostgreSQL healthy.

### Post-M13 — Expandable categories and functional catalog filters, блок 40

- «Ещё» в верхней навигации больше не открывает служебное дерево `/Catalog`: кнопка остаётся видимой на узких desktop-экранах, раскрывает оставшиеся восемь публичных корневых разделов на месте и меняется на «Свернуть». Публичный набор ограничен 15 приоритетными корнями и их потомками, поэтому накопленные admin/UI-test категории не попадают в шапку и пользовательский фильтр.
- При выборе корневого раздела появляется компактная горизонтальная строка прямых подкатегорий с пунктом «Все». При переходе в подкатегорию корневой chip и соответствующий пункт второй строки остаются активными; глубокая ветка использует уже существующий descendant search.
- Сортировка приведена к общей типографике/контролам и применяется сразу при выборе. Она сохраняет текущую категорию, быстрые/ценовые/динамические фильтры и режим выдачи; пустые query-параметры перед отправкой исключаются.
- Прежний выпадающий `details` заменён полноценным modal-диалогом фильтров: категория, диапазон цены, состояние, тип сделки, фото и dynamic attributes, кнопки применения/сброса, счётчик активных групп и закрытие по фону. Быстрые chips сохраняют текущий запрос и показывают active-state.
- Development featured seed теперь содержит осмысленное разнообразие фильтров и идемпотентно обновляет существующую БД: два объявления `New`, одно `Free`, одно `Exchange`, остальные `Used/FixedPrice`. Раньше все восемь карточек были `Used/FixedPrice`, поэтому корректно работающие «Новые» и «Бесплатно» всегда давали пустой результат.
- Browser regression покрывает раскрытие без навигации, 15 корней, подкатегории транспорта, active hierarchy, auto-submit сортировки, сохранение query и фактическое применение modal-фильтра. Search regression адаптирован к новому диалогу.
- Приёмка 2026-07-19: Debug build 0 ошибок/0 предупреждений; targeted catalog/search 3/3; полный Playwright набор 43/43; встроенный браузер подтвердил видимую кнопку «Ещё», 8 дополнительных разделов, отсутствие наложений/горизонтального overflow, рабочую строку подкатегорий, 2 результата «Новые» и 1 результат «Бесплатно»; Docker app/PostgreSQL healthy.

### Post-M13 — Theme-safe link and action palette, блок 41

- Для всех обычных и посещённых ссылок задана палитра Marketplace: коричневый акцент в светлой теме и светлый песочный акцент в тёмной. Стандартные browser blue/purple больше не могут появляться у неоформленной ссылки.
- Ссылки, оформленные как `.ui-button`, явно получают основной цвет текста и убирают подчёркивание; primary actions сохраняют белый текст. `select` и `textarea` вместе с `button/input` наследуют текущую типографику и цвет темы.
- В modal-фильтре «Сбросить всё» теперь имеет `#181818` в светлой теме и `#f5f1eb` в тёмной, primary «Показать» остаётся белой; обе темы визуально проверены во встроенном браузере.
- Новый design-system regression обходит видимые ссылки в обеих темах, запрещает стандартные blue/purple RGB и отдельно контролирует контраст reset action.
- Приёмка 2026-07-19: Debug build 0 ошибок/0 предупреждений; targeted palette 1/1; полный Playwright набор 44/44; Docker app/PostgreSQL healthy.

### Post-M13 — Incremental feed and unified list pagination, блок 42

- Главная больше не получает всю выдачу одним ответом: PostgreSQL/JSON каталоги поддерживают `Offset`/`Limit`, первый экран содержит не более 12 карточек, а JSON handler возвращает следующие части с медиа, избранным и корректными detail/action URL.
- `IntersectionObserver` подгружает очередные 12 объявлений примерно за 600 px до конца выдачи. Новые карточки сохраняют полноразмерную ссылку, hover-галерею, точки, избранное и текущие фильтры URL; предусмотрены состояния загрузки, повтора и полного завершения.
- Обычные длинные списки унифицированы клиентской пагинацией по 10 элементов: «Назад», номера страниц, «Вперёд» и диапазон `1–10 из N` работают без перезагрузки. Подключены рекомендации/история, сессии, диалоги, мои объявления, избранное, продавцы, модерация, финансы, operations и административные таблицы; главная и поток сообщений намеренно исключены.
- Карточки «Недавно просмотренные» уменьшены до пяти колонок на desktop, трёх на tablet и двух на mobile. Шаблон рекомендаций приведён к нормальной многострочной Razor-разметке, поэтому условный badge новых результатов больше не выводит исходный Razor-код.
- Сортировки по цене получили стабильный secondary order по дате публикации: свежий релевантный результат не теряется за границей первой порции при одинаковой цене.
- Добавлены regressions на server-sized infinite feed, отсутствие duplicate cards/потери query и постраничное переключение 23 элементов без навигации.
- Приёмка 2026-07-19: Debug build 0 ошибок/0 предупреждений; targeted catalog/design/search 20/20; полный Playwright набор 46/46; Docker app/PostgreSQL healthy.

### Post-M13 — Guest actions, notifications and end-to-end product integration, блок 43

- Публичная карточка объявления показывает продавца отдельным связанным блоком с городом, рейтингом и переходом в профиль; продавец также стал ссылкой в избранном и сравнении.
- Гость видит не скрытые действия, а три понятных CTA: «Войти, чтобы написать», «Войти и добавить в избранное», «Войти и сравнить». Каждый вход сохраняет локальный безопасный ReturnUrl, возвращает к тому же объявлению, показывает объяснение и визуально выделяет действие, которое осталось завершить.
- Гостевое сердечко главной теперь возвращает после входа не в безликую выдачу, а в конкретную карточку с подготовленным действием избранного. Авторизованные favorite/compare операции показывают явные success-сообщения.
- Создан отдельный `/Notifications`: изменения цены, сохранённые поиски и сделки объединены в одном списке, имеют unread-state, тип, дату, безопасный POST-переход с отметкой прочтения и действие «Прочитать все». Страница включена в account navigation и общую пагинацию.
- Избранное переработано в полноразмерные связанные карточки с продавцом, корректным отображением исходной цены только при реальном изменении и удалением на месте. Сравнение получило ссылки на продавцов, удаление столбца, подсказку при одном товаре и полезный пустой CTA.
- Пустые состояния сообщений, моих объявлений, избранного, сравнения и уведомлений теперь содержат конкретный следующий переход, а не только описание проблемы.
- Новый e2e проходит цепочку guest listing → sign-in → return intent → favorite → compare → seller profile. Price-drop regression переведён на настоящий notification center и проверяет unread/open flow.
- Приёмка 2026-07-19: Debug/Release build 0 ошибок/0 предупреждений; targeted integration 3/3; полный Playwright набор 47/47; встроенный браузер подтвердил desktop/mobile layout без горизонтального overflow; Docker app/PostgreSQL healthy.

### Post-M13 — Registration clarity and self-service lifecycle, блок 44

- Экран `/Account/SignIn` переименован во «Вход и регистрация» и заранее объясняет два сценария: существующий email входит после кода, новый email автоматически создаёт аккаунт. Технические тексты `fake-provider` и демонстрационного администратора удалены из пользовательского интерфейса; локальный код подписан как тестовый аналог письма.
- `/Admin/Users` получил явную колонку «Доступ к модерации»: администратор может назначить или снять роль `Moderator`, обязан указать причину, видит результат операции, а каждое реальное изменение пишется в аудит отдельным событием. Текущие роли показаны badge-элементами.
- В «Моих объявлениях» у каждой карточки появился раскрываемый опасный сценарий удаления с обязательным подтверждением. Удаление выполняется как auditable soft-delete `Deleted`, добавляет status history, удаляет объявление из избранного/сравнения и сразу скрывает его из личного списка и публичной карточки.
- В профиле второстепенная кнопка «Скачать мои данные» заменена основной опасной кнопкой «Удалить учётную запись». Она ведёт прямо в danger-zone страницы данных, где удаление оформляется существующим проверяемым запросом оператору; технический JSON-экспорт сохранён ниже как необязательная второстепенная функция.
- Identity regression проверяет понятное объяснение автосоздания аккаунта и отсутствие `fake-provider`; administration e2e назначает и снимает модератора; listings e2e удаляет собственный черновик и подтверждает HTTP 404 его прежней карточки.
- Приёмка 2026-07-19: Debug/Release build 0 ошибок/0 предупреждений; targeted identity/listings/admin 4/4; полный Playwright набор 47/47; встроенный браузер подтвердил профиль, danger-zone и удаления объявлений на desktop/mobile без horizontal overflow; Docker app/PostgreSQL healthy.

### Post-M13 — Real MapLibre/PMTiles integration, блок 45

- Источник базовой карты: отдельный локальный сервис `marketplace_maps_codex`, подключаемый через `MAPS_API_BASE_URL`. Браузер обращается только к same-origin `/maps-api`; PMTiles проксируются потоком с сохранением `Range`, `206`, `Content-Range`, `ETag` и cache headers.
- Источник объявлений на карте: только `MarketplaceDbContext`/PostgreSQL текущего проекта. Endpoint кластеров применяет bbox, zoom, категорию, поиск, цену, состояние, тип сделки и наличие фото; внешние demo listing ID не смешиваются с приложением.
- Fake map markup удалён. MapLibre-клиент строит локальный Protomaps style, создаёт DOM-кластеры/фотомаркеры, связывает маркеры с карточками, обновляется на `moveend` с отменой предыдущего запроса, перестраивает стиль при смене темы и оставляет список рабочим при деградации карты.
- Шаг «Детали» мастера объявления получил autocomplete адреса (350 мс, AbortController, клавиатура/ARIA). Точные координаты сохраняются отдельно; если пользователь не разрешил точный показ, публичная точка детерминированно смещается на 120–300 м.
- Проверено: Debug build 0/0; config BFF возвращает локальные rewritten URL; геокодер возвращает подсказки; PMTiles Range `bytes=0-1023` возвращает `206` и ровно 1024 байта; браузер показал MapLibre canvas, OSM attribution, реальные кластеры и маркеры; Docker app/PostgreSQL healthy.

### Post-M13 — Listing/profile UX repair, блок 46

- Исправлена первопричина повторяющихся полей игрового ноутбука: `LoadAttributesAsync` выбирает ровно одну последнюю опубликованную `CategorySchemaVersion`, а не объединяет атрибуты всех версий.
- Step 2 показывает адресный autocomplete карты, сохраняет exact/public координаты единообразно, валидирует фото по расширению/MIME/signature и сообщает ошибку вместо молчаливого пропуска. Step 3 показывает реально загруженные фото.
- Исправлены вертикальные отступы consent-блока/кнопок, CTA в «Моих объявлениях» и лишний `margin-top` у второй/третьей карточек истории.
- Личный nav упрощён: профиль первый, служебные «Роли» видят только Moderator/Administrator. Обычный Member не видит служебное управление каталогом.
- Добавлены `ApplicationUser.AvatarUrl`, миграция `ProfileAvatar`, интерактивное круглое кадрирование 512×512 с drag/zoom/accept и вывод аватара в личном/публичном профиле и блоке продавца.
- Поле города использует фильтруемый datalist поддерживаемых городов с серверной проверкой. Email OTP flow расширен нормализацией российских номеров и регистрацией по телефону; в локальной среде тот же fake OTP имитирует доставку кода, production всё ещё требует реального SMS-провайдера.
- Регрессии: identity/listings 4/4 и полный Playwright-набор 48/48, включая email, телефон, аватар, единственный набор атрибутов, адресную подсказку, загрузку/предпросмотр PNG и полный трёхшаговый сценарий. Docker app/PostgreSQL healthy.

### Post-M13 — All-Russia profile city search, блок 47

- Короткий `datalist` из пяти городов заменён на асинхронные подсказки подключённого геокодера карты: поле профиля ищет города и населённые пункты по всей России, поддерживает клавиатуру и показывает до восьми уточнённых вариантов.
- Профиль принимает изменённый город только после выбора подсказки, сохраняет нормализованное название и точные координаты в новых полях `ApplicationUser.CityLatitude/CityLongitude`; добавлена миграция `ProfileCityCoordinates`.
- При создании объявления без отдельного адреса стабильная публичная точка теперь рассчитывается от координат города профиля, поэтому дальние города больше не попадают в резервный центр Москвы.
- Приёмка 2026-07-19: Debug build 0 ошибок/0 предупреждений; targeted identity 1/1; полный Playwright-набор 48/48; встроенный браузер подтвердил подсказку «Владивосток, Приморский край»; Docker app/PostgreSQL healthy.

### Post-M13 — Runtime upload delivery repair, блок 48

- Устранена причина пустого аватара после сохранения: endpoint-ориентированный `MapStaticAssets` знает только файлы манифеста сборки и не раздавал изображения, созданные во время работы приложения из Docker volume.
- Для изолированного `/uploads` подключён физический static-file provider с корректными MIME, Range/ETag и immutable cache header; существующий файл аватара сразу стал доступен без повторной загрузки. Это одновременно исправляет runtime-фотографии объявлений.
- Identity e2e теперь проверяет `naturalWidth > 0`, HTTP `200` и `image/jpeg`, поэтому сломанный `<img>` больше не считается успешным отображением.
- Приёмка 2026-07-19: Debug build 0 ошибок/0 предупреждений; targeted identity 1/1 и полный Playwright-набор 48/48; реальный ранее сохранённый аватар возвращает 200 и декодируется как 512×512; Docker app/PostgreSQL healthy.

### Post-M13 — Listing media cache repair, блок 49

- Повторён полный пользовательский сценарий нового объявления: выбор локального PNG, multipart POST второго шага, сохранение файла/`ListingMedia`, предпросмотр, публикация и открытие галереи. Изображение реально декодируется на обоих экранах и возвращается с `200 image/png`.
- Причиной продолжающегося отображения пустых фотографий после блока 48 оказался закэшированный браузером прежний `404` на неизменившемся media URL. Миграция `RuntimeUploadCacheBust` добавила версию ко всем старым `/uploads/...` URL; новые загрузки получают уникальную временную версию сразу при сохранении.
- Непосредственно проверено пользовательское объявление «Еуец»: две записи `ListingMedia`, два файла в volume, обе фотографии декодируются и переключаются в галерее; после миграции первая загружается по новому URL с реальным размером 222×227.
- Listings e2e усилен проверками `naturalWidth > 0`, HTTP `200`, `image/png` и фактического изображения в detail gallery. Приёмка: targeted 1/1, полный Playwright-набор 48/48, Docker app/PostgreSQL healthy.

### Post-M13 — Map runtime and controls repair, блок 50

- Серый экран диагностирован как неинициализированный fallback карты: новая fingerprint-версия MapLibre bundle исключает использование старого закэшированного скрипта, а статическая надпись города теперь скрывается только после настоящего события `map.load`.
- «Искать при перемещении» больше не обрабатывается одновременно двумя скриптами и не переключается обратно. Состояние `aria-pressed` меняется реально, управляет viewport-запросами и сохраняется в `localStorage`.
- «Показать списком» теперь переключает не только мобильный `map-mode`, но и desktop-контейнер из `mode-map` в `mode-list`, показывает карточки, скрывает карту и синхронизирует URL `mode=list` без перезагрузки.
- Ошибка карты показывает понятный status и кнопку «Повторить». Проверены same-origin config, серверные кластеры, health геосервиса и PMTiles Range 206/1024 байта.
- Новый desktop e2e проверяет `map-ready`, canvas, реальные маркеры, скрытие fallback, переключатель и возврат к списку. Приёмка: map regressions 4/4, полный Playwright-набор 49/49; встроенный браузер подтвердил canvas, 4 маркера и обе кнопки; Docker app/PostgreSQL healthy.

### Post-M13 — My Listings view history and administrator assignment, блок 51

- Внизу `/Listings/My`, после собственных объявлений и явного разделителя, добавлен блок «Недавно просмотренные». Он показывает активные объявления за последние 30 дней компактной сеткой 5/3/2 карточки, поддерживает изображения/placeholder, дату, город и переход в карточку.
- Для истории предусмотрены понятные состояния: пустая история с переходом в каталог и выключенная персонализация со ссылкой на профиль. При количестве больше 10 включается существующая унифицированная пагинация без перезагрузки; управление очисткой остаётся на `/Recommendations`.
- Regression создаёт настоящий view event и подтверждает его отображение именно в «Моих объявлениях», после чего проверяет прежнее управление рекомендациями.
- Точно проверенный аккаунт `ondvfx@gmail.com` получил дополнительную роль `Administrator` с idempotent вставкой и событием `identity.role-assigned` в `AuditEvents`. Повторный вход подтвердил ссылки «Роли»/«Администрирование» и доступ к `/Admin`.
- Приёмка: Debug build 0 ошибок/0 предупреждений; targeted recommendations 1/1; полный Playwright-набор 49/49; встроенный браузер подтвердил новый раздел и административную платформу; Docker app/PostgreSQL healthy.

### Post-M13 — Unified empty-state spacing, блок 52

- Общий `.state-panel` получил единый внутренний flex-контейнер и сброшенные отступы `h1/h2/h3`: пояснение теперь относится к заголовку визуально (6 px), а CTA отделён от пояснения на 20 px.
- Изменение автоматически применяется к сообщениям («Откройте активное объявление…»), избранному, уведомлениям, сравнению, моим объявлениям, истории, сохранённым поискам, каталогу, access denied и component gallery без локальных расхождений.
- Добавлена геометрическая Playwright-регрессия, проверяющая обе дистанции; light/dark visual baselines обновлены после ожидаемого увеличения высоты.
- Приёмка: Debug build 0 ошибок/0 предупреждений; targeted spacing/visual 2/2; полный Playwright-набор 50/50; браузерная проверка пустого избранного измерила 6 px и 20 px; Docker app/PostgreSQL healthy.

### Post-M13 — Listing breadcrumbs to filtered marketplace, блок 53

- `ListingView` расширен slug текущей категории. В detail breadcrumbs техническая ссылка `/Catalog` заменена на «Все объявления» → `/`, а название категории стало ссылкой `/?category=<slug>`.
- Категорийная ссылка использует существующий поиск `DatabaseListingCatalog`, который включает выбранную категорию и всех её потомков: одинаково работают корневые разделы вроде «Бытовая техника» и глубокие ветки вроде игровых ноутбуков.
- Fallback-ссылка демонстрационной карточки на главной также больше не открывает технический preview схем и возвращает в рабочую выдачу.
- Regression проверяет оба href, переход по категории, параметр URL и заголовок отфильтрованной выдачи. Встроенный браузер подтвердил `/` и `/?category=bytovaya-tehnika` на реальной карточке «Еуец», без технического preview.
- Приёмка: Debug build 0 ошибок/0 предупреждений; targeted catalog 1/1; полный Playwright-набор 50/50; Docker app/PostgreSQL healthy.

### Post-M13 — Stable header alignment across navigation, блок 54

- Причиной вертикального скачка была разная высота двух вариантов шапки: `.header-main` на главной 92 px, `.account-header__inner` в профиле 76 px при одинаковом логотипе 48 px. Личная шапка приведена к 92 px, расчёт минимальной высоты account page обновлён.
- Все ссылки `.account-nav` получили фиксированную высоту 48 px, `inline-flex` и вертикальное центрирование — их центральная ось совпадает с логотипом, переключателем темы и элементами главной навигации.
- Identity e2e теперь измеряет координаты до/после перехода: верх логотипа главной и профиля совпадает с допуском 1 px, центры навигации и логотипов также совпадают.
- Приёмка: Debug build 0 ошибок/0 предупреждений; targeted identity 1/1; полный Playwright-набор 50/50; живая браузерная навигация главная → профиль подтверждена; Docker app/PostgreSQL healthy.

### Post-M13 — Full-address marketplace location, блок 55

- В главной шапке и диалоге географии термин «Город» заменён на «Адрес». Поле принимает город, улицу и дом, показывает до восьми подсказок подключённого российского геокодера, поддерживает клавиатуру и требует выбрать нормализованный вариант с координатами.
- Выбранные полный адрес, широта и долгота сохраняются в отдельных cookie на год и восстанавливаются после перезагрузки. Длинный адрес безопасно сокращается многоточием в шапке, полный текст доступен в `title`; карта показывает тот же адрес.
- `CatalogSearchRequest`, база данных и JSON fallback получили координатный центр. При выбранном адресе объявления фильтруются фактическим расстоянием от координат в заданном радиусе, а не ограниченным списком из пяти городов; для PostgreSQL сначала применяется ограничивающий bbox, затем точная формула расстояния.
- Адрес и координаты сохраняются при глобальном и сохранённом поиске, открытии фильтров, сортировке, переключении карты и динамической подгрузке. Геолокация также записывает координаты в тот же формат; при отказе интерфейс предлагает заполнить адрес вручную.
- Geography e2e обновлён на полный адрес с контролируемой подсказкой геокодера и проверкой сохранения после reload. Приёмка: Debug build 0 ошибок/0 предупреждений; targeted geography 3/3; полный Playwright-набор 50/50; живая браузерная проверка выбрала адрес «Тверская, 12, Козицкий переулок, Тверской, Москва, 125009», сохранила координаты и показала 8 объявлений; Docker app/PostgreSQL healthy.

### Post-M13 — Effective-price sorting, блок 56

- Сортировка больше не использует сохранённое числовое поле цены напрямую для сделки `Free`: вычисляемая цена бесплатного объявления всегда равна 0, даже если объявление ранее было переведено из платного режима и в базе осталось старое число.
- В режиме «Сначала дешёвые» бесплатные объявления идут первыми, затем платные по возрастанию, затем объявления без цены. В режиме «Сначала дорогие» платные идут по убыванию, бесплатные — после них, а объявления без указанной цены остаются в конце.
- Одинаковая логика применена к PostgreSQL-каталогу и JSON fallback. Добавлена e2e-регрессия для обоих направлений сортировки; targeted catalog suite 14/14, полный Playwright-набор 51/51, Debug build 0 ошибок/0 предупреждений, Docker app/PostgreSQL healthy.

### Post-M13 — Removable newest quick filter, блок 57

- Причина неснимаемого «Сначала свежие»: активная ссылка удаляла `sort` из URL, после чего `IndexModel` законно восстанавливал `newest` из годовой cookie `marketplace.search-sort`.
- Повторное нажатие теперь явно передаёт `sort=recommended`, обновляет cookie, снимает active-state и синхронизирует основной список сортировки. Общая ссылка «Сбросить» также передаёт рекомендованную сортировку, поэтому сохранённая свежесть не возвращается.
- Добавлен e2e-сценарий включения и повторного отключения быстрого фильтра с проверкой URL, active-класса и значения combobox. Приёмка: Debug build 0 ошибок/0 предупреждений; targeted catalog suite 15/15; полный Playwright-набор 52/52; Docker app/PostgreSQL healthy.

### Post-M13 — Real map canvas and asset repair, блок 58

- Живая диагностика в открытом встроенном браузере воспроизвела дефект, который прежний e2e не ловил: оболочка имела высоту, но `[data-map-canvas]` был `0×0` и скрыт responsive-правилом при серверном `mode=map`; консоль MapLibre дополнительно показывала `Invalid sprite URL` для относительного пути.
- Порядок CSS исправлен: MapLibre stylesheet загружается до проектного `site.css`, а проектный canvas селектор с повышенной специфичностью задаёт `position:absolute`, `width/height:100%`. Серверный `.catalog-layout.mode-map` явно показывает карту и на ширинах до 1199 px.
- Пути PMTiles, glyphs и sprite нормализуются в абсолютные URL до передачи MapLibre. После события `load` выполняется обязательный `resize`; ошибочная проверка `map.loaded()` удалена из refresh, поскольку она могла отменить самый первый запрос кластеров непосредственно внутри обработчика `load`.
- Новый e2e воспроизводит ширину встроенного браузера 858×912, требует видимую оболочку, ненулевой canvas больше 500×500, маркеры и отсутствие ошибок sprite/PMTiles. Приёмка: targeted catalog+geography 19/19; полный Playwright-набор 53/53; живая проверка — shell 811×720, canvas 809×718, 5 маркеров, атрибуция OSM/MapLibre и визуально прорисованная Москва; Docker app/PostgreSQL healthy.

### Post-M13 — Private full address in profile, блок 59

- Профиль больше не редактирует публичный `City` как единственную географию. В `ApplicationUser` добавлено отдельное nullable-поле `Address` до 500 символов и миграция `ProfileFullAddress`; прежний город сохранён для публичной карточки продавца и совместимости объявлений.
- Поле профиля переименовано в «Адрес», принимает город, улицу и дом, сохраняет полную строку выбранной подсказки и координаты. Валидация и ошибки переведены на адресную терминологию; строка помечена как приватная и не публикуется автоматически.
- При сохранении адреса публичный город извлекается из нормализованных компонентов без региона, индекса и улицы. Собственный JSON-экспорт включает полный адрес и координаты, поскольку это данные владельца аккаунта.
- Мастер нового объявления использует адрес и координаты профиля как исходное значение на адресном втором шаге, если у черновика ещё нет собственного адреса; пользователь может заменить его отдельной подсказкой.
- Identity e2e использует детерминированную подсказку полного владивостокского адреса и проверяет полную строку, координаты, повторное открытие и экспорт. Приёмка: Debug build 0 ошибок/0 предупреждений; targeted identity/listings 4/4; полный Playwright-набор 53/53; Docker app/PostgreSQL healthy.

### Post-M13 — MapLibre resize synchronization, блок 60

- Устранён риск пустой полосы карты после изменения масштаба браузера или ширины панели. Прежний `ResizeObserver` следил только за внутренним `[data-map-canvas]`, поэтому отдельные изменения внешней раскладки и visual viewport могли не приводить к своевременному обновлению пиксельного буфера MapLibre.
- Единая функция синхронизации теперь наблюдает оболочку карты, canvas и родительский layout, реагирует на `window.resize`, `visualViewport.resize`, смену ориентации и окончание CSS-переходов. Частые события объединяются в двойной `requestAnimationFrame`, после завершения layout вызывается `map.resize()` только для видимого контейнера ненулевого размера.
- Добавлен e2e-сценарий 1100 → 760 → 1100 px, который на каждом размере сравнивает геометрию контейнера, CSS-canvas и его фактический пиксельный буфер. Приёмка: Debug build 0 ошибок/0 предупреждений; targeted catalog+geography 20/20; полный Playwright-набор 54/54; живая проверка встроенного браузера — 1051×718 → 719×648 → 1051×718 без зазора, 5 маркеров сохраняются; Docker app/PostgreSQL healthy.

### Post-M13 — Stable labeled map markers, блок 61

- Найдена общая причина двух визуальных дефектов: MapLibre непрерывно записывает географическое положение маркера в inline `transform`, а перенесённые из старой fake-map стили анимировали этот же `transform` 160 мс и заменяли его через `!important` при hover. Поэтому маркер догонял карту при перетаскивании и прыгал в другую точку при наведении.
- Реальные маркеры отделены от legacy-класса `.map-marker`. Изменение координат больше не имеет CSS-transition, а hover/связь с карточкой используют только рамку и тень, не меняющие размер или положение маркера.
- Возвращён стиль шаблона: маркер объявления — компактная светлая/тёмная плашка с круглой фотографией и подписью цены; полное название и цена доступны в `aria-label` и tooltip. Регрессия требует непустую подпись, отсутствие `transform` среди transition-свойств и неизменность центра маркера при hover с допуском менее 1 px.
- Приёмка: Debug build 0 ошибок/0 предупреждений; targeted catalog 17/17; полный Playwright-набор 54/54; живая карта показывает 5 маркеров, первый — 123×46 px с подписью цены, а координатный inline-transform остаётся под полным контролем MapLibre; Docker app/PostgreSQL healthy.

### Post-M13 — Continuous PMTiles rendering and durable map service, блок 62

- Серые прямоугольники на больших масштабах локализованы до отдельных векторных тайлов: архив PMTiles и диапазонные ответы сервера были целыми, но сложный автоматически созданный слой дорожных подписей заставлял MapLibre отбрасывать некоторые насыщенные тайлы целиком. Клиент переведён на проверенный безопасный стиль с непрерывным фоном, дорогами, границами, железными дорогами и подписями населённых пунктов; проблемные дорожные подписи исключены.
- Картографический backend добавлен в основной `compose.yaml` как `maps-api` с healthcheck и `restart: unless-stopped`. Архив и ассеты подключаются read-only, локальная раздача поддерживает HTTP Range и запрещает хранить ошибочные ответы в браузерном кеше. После перезагрузки ПК достаточно запуска Docker Compose — отдельный ручной процесс карты больше не требуется.
- Центр карты корректно берётся из `latitude`/`longitude` только при фактическом наличии параметров; пустой URL больше не превращается в координаты 0,0. Наблюдение за размером ограничено оболочкой и вызывает `map.resize()` только при реальном изменении геометрии, поэтому не создаёт лишний `moveend` и повторную замену маркеров.
- Связь карточки с маркером стала устойчивой к обновлению кластеров, а hover-подсветка окончательно лишена масштабирования: центр маркера не меняется и объявление не «упрыгивает».
- Приёмка: Debug build 0 ошибок/0 предупреждений; targeted catalog+geography 20/20; полный Playwright-набор 54/54. Светлая и тёмная карта визуально проверены на масштабах 13 и 15 без серых прямоугольных разрывов; Docker web/maps-api/PostgreSQL healthy.

## Следующие действия

1. Подключить secret manager, HTTPS/reverse proxy/DNS, реальный OTP provider, внешний `/metrics` collector и off-site backups/PITR.
2. Провести внешний security/pentest и юридическую проверку; реальные платежи не включать до provider onboarding и утверждения финансового процесса.

## Важные технические решения

- Код входа намеренно отображается на странице только в локальном fake-provider. В production его нужно заменить асинхронной доставкой email/SMS и секретным хранилищем кодов.
- У cookie есть claim `session_id`; проверка активной сессии идёт при валидации cookie. Отзыв сессии лишает её доступа на следующем запросе.
- EF CLI закреплён как локальный tool в `.config/dotnet-tools.json`.
- M3: сущности `CatalogCategory`, `CategorySchemaVersion`, `CategoryAttribute`; миграция `CatalogStructure`; seed создаёт 15 верхних разделов и глубокие ветки четырёх демонстрационных доменов. Публичный экран `/Catalog`, admin экран `/Admin/Catalog` доступен только роли Administrator.
- M4: сущности `Listing`, `ListingRevision`, `ListingMedia`, `ListingPriceHistory`, `ListingAttributeValue`, `ListingStatusHistory`; миграция `ListingsWorkflow` применена.
- M4 UI: `/Listings/Create` — трёхшаговый мастер с сохранением черновика, dynamic fields и preview; `/Listings/My` — список объявлений. Публикация запускает M5 moderation engine; изменения цены пишутся в `ListingPriceHistory`, другие основные изменения — в `ListingRevision`.

### M4 — Создание и управление объявлениями

- Приёмка 2026-07-17: Release build без ошибок/предупреждений; 17 Playwright-тестов; сквозной сценарий подтверждает три шага, атрибуты, историю цены, ревизии и пересоздание при смене категории.

### M3 — Каталог и seed-структура

- Приёмка 2026-07-17: Release build без ошибок/предупреждений; 16 Playwright-тестов; admin добавляет подкатегорию без пересборки; версии схем создаются с копированием атрибутов; Docker и PostgreSQL healthy.
