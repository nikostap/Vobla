$ErrorActionPreference = "Stop"
$workspacePath = Split-Path -Parent $PSScriptRoot
$backupDir = Join-Path $workspacePath "artifacts\backups"
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $backupDir "marketplace-$stamp.sql"
docker compose -f (Join-Path $workspacePath "compose.yaml") exec -T postgres pg_dump -U marketplace -d marketplace --clean --if-exists | Set-Content -LiteralPath $backupPath -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw "pg_dump failed" }
$drillDb = "marketplace_restore_drill"
docker compose -f (Join-Path $workspacePath "compose.yaml") exec -T postgres psql -U marketplace -d postgres -c "DROP DATABASE IF EXISTS $drillDb WITH (FORCE);"
docker compose -f (Join-Path $workspacePath "compose.yaml") exec -T postgres psql -U marketplace -d postgres -c "CREATE DATABASE $drillDb OWNER marketplace;"
Get-Content -LiteralPath $backupPath | docker compose -f (Join-Path $workspacePath "compose.yaml") exec -T postgres psql -U marketplace -d $drillDb | Out-Null
$tableCount = docker compose -f (Join-Path $workspacePath "compose.yaml") exec -T postgres psql -U marketplace -d $drillDb -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"
if ([int]$tableCount -lt 10) { throw "Restore validation failed: only $tableCount tables" }
docker compose -f (Join-Path $workspacePath "compose.yaml") exec -T postgres psql -U marketplace -d postgres -c "DROP DATABASE $drillDb WITH (FORCE);"
Write-Output "Backup: $backupPath"
Write-Output "Restore drill passed: $tableCount public tables"
