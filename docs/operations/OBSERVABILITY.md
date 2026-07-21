# Observability

## Correlation ID

Каждый HTTP-ответ содержит `X-Correlation-ID`. Клиентский идентификатор принимается только как GUID; при отсутствии или неверном формате приложение создаёт новый. Тот же идентификатор записывается в `HttpContext.TraceIdentifier`, scope структурированного лога и существующие audit events.

При разборе инцидента передайте пользователю correlation ID из ответа и ищите его в централизованных логах. Не используйте correlation ID как секрет или средство авторизации.

## Prometheus endpoint

`GET /metrics` публикует Prometheus text format 0.0.4 и содержит:

- `marketplace_http_requests_total` — завершённые запросы;
- `marketplace_http_active_requests` — текущие запросы;
- `marketplace_http_request_duration_seconds_sum` — накопленное время обработки;
- `marketplace_http_responses_total{status_class="2xx"}` — ответы по классу статуса;
- `marketplace_background_jobs_total{job,status}` — результаты попыток maintenance worker;
- `marketplace_background_job_last_completion_timestamp_seconds` — время последнего терминального результата worker.
- `marketplace_auth_events_total{action,outcome}` — выдача/проверка OTP и срабатывания rate limiter с фиксированными значениями labels.

Labels намеренно ограничены фиксированными значениями. В них нет URL, пользователя, IP и correlation ID.

Неверная OTP-проверка дополнительно создаёт structured warning с remote IP внутри correlation scope. Email и введённый код в лог не записываются. Рекомендуемое оповещение: резкий рост `marketplace_auth_events_total{action="verify",outcome="failed"}` или `outcome="rate_limited"`.

В Development endpoint доступен напрямую. В Production требуется заголовок `Authorization: Bearer <token>`, где секрет задаётся через `Observability__MetricsToken` и содержит не менее 24 символов. Не публикуйте endpoint в интернет: ограничьте его внутренней сетью/reverse proxy и храните токен в secret manager.

Пример scrape-конфигурации:

```yaml
scrape_configs:
  - job_name: marketplace
    metrics_path: /metrics
    authorization:
      type: Bearer
      credentials_file: /run/secrets/marketplace_metrics_token
    static_configs:
      - targets: ["marketplace-web:8080"]
```

## Минимальные оповещения

- `/health/ready` не отвечает 200 более двух минут;
- доля 5xx за 5 минут превышает согласованный SLO;
- нет успешного `history-retention` более 26 часов;
- нет успешного `saved-search-matches` более 30 минут;
- растёт число `failed` maintenance jobs.

Счётчики хранятся в памяти процесса и сбрасываются при рестарте. Для истории, агрегации между репликами и alerting обязателен внешний Prometheus-совместимый сборщик.
