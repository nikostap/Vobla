# Incident runbook

Локальная/CI проверка реакции на недоступность БД: `./scripts/readiness-failure-drill.ps1`. Скрипт временно останавливает только PostgreSQL service, не удаляет volume и всегда пытается восстановить stack в `finally`.

1. Confirm impact with `/health` and `/health/ready`; record start time and correlation IDs.
2. Freeze risky admin operations by disabling the relevant feature flag; do not delete evidence.
3. Inspect application logs, audit events, active sessions and recent background jobs.
4. For account compromise, revoke sessions and preserve IP/device/audit records.
5. For data integrity, take a PostgreSQL backup before repair and validate it in an isolated restore database.
6. Communicate scope and workaround; never include private messages, exact coordinates, OTPs or secrets.
7. Recover, verify critical UI journeys, monitor recurrence, then write a blameless post-incident review with corrective actions.

Severity: SEV-1 unavailable/data exposure, SEV-2 major workflow loss, SEV-3 limited degradation. Escalate SEV-1 immediately to Owner/SecuritySpecialist.
