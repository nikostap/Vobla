# Deployment readiness

Health topology: `/health/live` проверяет только работоспособность процесса и не должен использоваться для снятия трафика; `/health/ready` зависит от PostgreSQL и предназначен для readiness/load-balancer. Совместимый `/health` выполняет все зарегистрированные проверки.

## Реализовано после M13

- maintenance jobs хранятся в PostgreSQL и обрабатываются `MaintenanceJobWorker` со статусами `Queued/Running/Completed/Failed`, восстановлением stale lock и тремя попытками с backoff;
- recurring scheduling защищён PostgreSQL advisory transaction lock, а multi-replica claim выполняется атомарно через `FOR UPDATE SKIP LOCKED`;
- history retention планируется раз в сутки, saved-search matching — раз в 15 минут;
- Production запускается только с явными hosts, закрытой регистрацией, сильным invite secret и недемонстрационным подключением к БД;
- production cookie требует HTTPS, HttpOnly и SameSite=Lax;
- контейнер работает как non-root, без capabilities, с `no-new-privileges` и read-only root filesystem;
- chat files, listing uploads и data-protection keys находятся в отдельных persistent volumes;
- Docker healthcheck использует `/health/ready` и поэтому учитывает PostgreSQL.
- каждый ответ получает безопасный `X-Correlation-ID`, совпадающий с trace identifier и scope структурированного лога;
- `/metrics` предоставляет низкокардинальные HTTP/worker метрики и в Production закрыт обязательным bearer token не короче 24 символов.
- OTP endpoint разделён на per-IP лимиты просмотра/выдачи/проверки, возвращает `429` и `Retry-After`; один код допускает не более пяти неверных попыток, а повторная fake-выдача на email подавляется одну минуту;
- автоматическое назначение Administrator по demo email работает только в Development; `Identity__DemoAdminEmail` в Production обязан быть пустым.
- forwarded client IP/protocol принимаются только от одного явно разрешённого proxy hop; Production требует хотя бы один `ReverseProxy__KnownProxies__N` с точным IP reverse proxy.
- HTTP body budget: 1 МБ по умолчанию, 65 МБ для `/Messages/Chat`, 105 МБ для `/Listings/Create`; Kestrel имеет тот же абсолютный потолок 105 МБ и ограниченные headers/timeouts.
- публичный динамический HTML/health/metrics поддерживает Brotli/Gzip; персональные и auth paths не сжимаются, fingerprinted static assets имеют immutable cache.
- uploads проверяются по count/size/extension/MIME и magic bytes/strict UTF-8 до сохранения; внешний antivirus/content-disarm adapter остаётся обязательным production provider.
- незавершённые atomic-upload temp-файлы старше часа очищаются durable job; завершённые и свежие файлы не затрагиваются.
- первый production Owner создаётся только отдельной deployment-командой `dotnet Marketplace.Web.dll --bootstrap-owner`. Email, отображаемое имя и одноразовый секрет читаются из `Bootstrap__OwnerEmail`, `Bootstrap__OwnerDisplayName`, `Bootstrap__OwnerToken`; секрет не передаётся через argv, должен содержать минимум 32 символа и удаляется из secret manager сразу после успеха. Команда сериализована PostgreSQL advisory lock, идемпотентна для того же адреса, запрещает второго Owner и пишет `identity.owner.bootstrapped` в аудит. Обычный Production web startup отвергает оставшийся bootstrap-токен.

## Остаётся внешней инфраструктурной работой

- secret manager и ротация секретов;
- TLS termination, reverse proxy и production DNS;
- реальный email/SMS provider вместо fake OTP;
- зарегистрированный production adapter должен реализовать `IOneTimeCodeService` и выбираться через `Identity__OtpProvider`; Fake или неизвестное значение блокируют startup;
- внешний Prometheus-совместимый сборщик, централизованные traces/logs и оповещения по `OBSERVABILITY.md`;
- off-site encrypted backups/PITR и регулярное расписание restore drill;
- внешний pentest и юридическая приёмка.
