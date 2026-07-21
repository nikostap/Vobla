# 8. Техническая архитектура

## 8.1. Стек

- .NET и ASP.NET Core;
- C#;
- Razor Pages или MVC для серверного UI;
- Razor Components только для локально интерактивных зон;
- TypeScript для карты, загрузки файлов и сложного поведения;
- SignalR для real-time;
- Entity Framework Core;
- PostgreSQL;
- PostGIS;
- Redis при необходимости;
- S3-совместимое хранилище;
- Docker Compose для Development и Staging;
- OpenTelemetry для наблюдаемости.

Целевая версия .NET фиксируется в репозитории через `global.json` и обновляется отдельным решением.

## 8.2. Архитектурный стиль

Модульный монолит. Один deployable web-продукт и отдельные worker-процессы, но доменные модули имеют явные границы.

Не создавать микросервисы, пока нет измеримой причины: независимого масштабирования, разной доступности, отдельной команды или тяжёлой технологической нагрузки.

## 8.3. Рекомендуемая структура solution

```text
Marketplace.sln
src/
  Marketplace.Web/
  Marketplace.Api/
  Marketplace.Workers/
  Marketplace.AppHost/              # optional orchestration
  Marketplace.SharedKernel/
  Marketplace.Contracts/
  Modules/
    Identity/
      Domain/
      Application/
      Infrastructure/
      Presentation/
    Users/
    Catalog/
    Listings/
    Media/
    Geo/
    Search/
    Moderation/
    Messaging/
    Deals/
    Reviews/
    Favorites/
    Comparisons/
    Recommendations/
    Billing/
    Promotions/
    Notifications/
    Administration/
    Analytics/
tests/
  UnitTests/
  IntegrationTests/
  ArchitectureTests/
  FunctionalTests/
  UiTests/
```

Допустим более компактный физический layout на первых этапах, но пространства имён и зависимости должны отражать модули.

## 8.4. Правила зависимостей

- Domain не зависит от Infrastructure и Web.
- Модули не изменяют таблицы друг друга напрямую.
- Межмодульные вызовы идут через application contracts или integration events.
- Внешние провайдеры закрыты интерфейсами.
- Обработчики не возвращают EF entities наружу.
- API использует версионированные contracts.

## 8.5. Транзакции и события

- локальная транзакция внутри модуля;
- Outbox для надёжных событий;
- идемпотентные consumers;
- correlation id;
- optimistic concurrency для редактирования;
- отдельная ревизия объявления на модерации.

## 8.6. PostgreSQL

Используется для:

- пользователей;
- каталога;
- объявлений;
- чатов;
- модерации;
- платежей;
- аудита;
- полнотекстового поиска первой версии;
- географических операций через PostGIS.

Индексы проектируются по реальным запросам. Миграции выполняются автоматически только в Development; в Staging/Production — управляемым deployment step.

## 8.7. Поиск

### Фаза 1

- PostgreSQL full-text;
- trigram;
- словари синонимов в приложении или БД;
- PostGIS;
- materialized/search projection;
- фоновые обновления индекса.

### Фаза 2

OpenSearch, если:

- PostgreSQL не выдерживает измеренную нагрузку;
- нужны сложные фасеты;
- нужны продвинутые подсказки;
- ранжирование становится трудно поддерживать;
- требуется визуальный поиск.

PostgreSQL остаётся источником истины.

## 8.8. Карты

Интерфейсы:

```csharp
public interface IMapProvider { }
public interface IGeocodingProvider { }
public interface IReverseGeocodingProvider { }
public interface IIpGeolocationProvider { }
```

Первая реализация — Яндекс, но DTO провайдера не должны проникать в доменную модель.

## 8.9. Файлы

Процесс:

1. Создать временный upload session.
2. Выдать signed upload URL.
3. Загрузить в quarantine bucket.
4. Проверить размер, MIME, сигнатуру и вирусы.
5. Выполнить OCR/модерацию.
6. Удалить метаданные.
7. Создать thumbnails.
8. Перенести в public/private bucket.
9. Привязать к ревизии объявления или сообщению.

## 8.10. SignalR

Используется для:

- сообщений;
- typing indicator;
- presence;
- read receipts;
- уведомлений;
- будущего WebRTC signaling.

Состояние чата хранится в БД. SignalR не является источником истины. При нескольких экземплярах приложения подключается Redis backplane или управляемый аналог, после нагрузочного теста.

## 8.11. Фоновые задачи

- обработка изображений;
- автоматическая модерация;
- отправка уведомлений;
- обновление поисковой проекции;
- истечение и продление объявлений;
- рекомендации;
- очистка временных файлов;
- webhook retries;
- отчёты;
- резервное обслуживание.

На старте допустим `BackgroundService` или Hangfire. Выбор фиксируется ADR.

## 8.12. API

Версионирование:

```text
/api/v1/auth
/api/v1/users
/api/v1/categories
/api/v1/listings
/api/v1/search
/api/v1/map
/api/v1/chats
/api/v1/deals
/api/v1/favorites
/api/v1/comparisons
/api/v1/admin
```

Web UI может использовать application layer напрямую или внутренний API, но mobile-ready contracts должны существовать для ключевых сценариев.

## 8.13. Feature flags

Обязательные флаги:

- real payments;
- paid promotions;
- safe deal;
- delivery;
- identity verification;
- voice messages;
- transcription;
- internet calls;
- auto publish trusted listings;
- image search;
- child accounts;
- dark theme rollout.

## 8.14. Среды

### Development

- локальный Docker Compose;
- seed data;
- fake email/SMS;
- fake map adapter when needed;
- fake payment provider.

### Staging

- постоянно доступный URL;
- максимально близкая конфигурация;
- тестовые интеграции;
- миграции;
- демонстрация после каждого этапа.

### Production

- российский дата-центр;
- отдельные secrets;
- резервные копии;
- мониторинг;
- ограниченный доступ;
- отдельный административный домен.

## 8.15. Наблюдаемость

- structured logs;
- OpenTelemetry traces;
- metrics;
- error tracking;
- health checks;
- dashboard фоновых задач;
- алерты;
- аудит бизнес-событий;
- synthetic check ключевых страниц.

## 8.16. Резервные копии

- ежедневная полная копия;
- point-in-time recovery после production-ready настройки;
- 30 дневных копий;
- 12 месячных копий;
- отдельное хранилище;
- регулярная проверка восстановления.

## 8.17. Тесты

- unit tests домена;
- integration tests PostgreSQL;
- architecture tests зависимостей;
- functional API tests;
- Playwright UI tests;
- visual regression для ключевых экранов;
- load tests поиска, карты и SignalR перед production;
- security tests загрузок и auth.

## 8.18. ADR

Ключевые решения сохраняются в `docs/adr`:

- Razor Pages vs MVC;
- BackgroundService vs Hangfire;
- структура динамических атрибутов;
- PostgreSQL search projection;
- карта и геоячейки;
- модерационный pipeline;
- стратегия хранения чата;
- платёжный провайдер.
