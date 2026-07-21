$ErrorActionPreference = "Stop"
$workspacePath = Split-Path -Parent $PSScriptRoot
$dllPath = [System.IO.Path]::Combine($workspacePath, "src", "Marketplace.Web", "bin", "Release", "net10.0", "Marketplace.Web.dll")
$primaryEmail = "bootstrap-owner-smoke@marketplace.local"
$secondEmail = "bootstrap-owner-second@marketplace.local"
$savedEnvironment = @{
    AspNetCore = $env:ASPNETCORE_ENVIRONMENT
    Connection = $env:ConnectionStrings__Marketplace
    Email = $env:Bootstrap__OwnerEmail
    DisplayName = $env:Bootstrap__OwnerDisplayName
    Token = $env:Bootstrap__OwnerToken
}

function Remove-SmokeUsers {
    $sql = @"
BEGIN;
DELETE FROM "AuditEvents" WHERE "UserId" IN (SELECT "Id" FROM "AspNetUsers" WHERE "Email" IN ('$primaryEmail', '$secondEmail'));
DELETE FROM "AspNetUserRoles" WHERE "UserId" IN (SELECT "Id" FROM "AspNetUsers" WHERE "Email" IN ('$primaryEmail', '$secondEmail'));
DELETE FROM "AspNetUsers" WHERE "Email" IN ('$primaryEmail', '$secondEmail');
COMMIT;
"@
    $sql | docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U marketplace -d marketplace | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Unable to clean bootstrap smoke users." }
}

try {
    if (-not (Test-Path -LiteralPath $dllPath)) { dotnet build ([System.IO.Path]::Combine($workspacePath, "Marketplace.slnx")) -c Release | Out-Null }
    Remove-SmokeUsers
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ConnectionStrings__Marketplace = "Host=localhost;Port=5433;Database=marketplace;Username=marketplace;Password=marketplace_dev"
    $env:Bootstrap__OwnerEmail = $primaryEmail
    $env:Bootstrap__OwnerDisplayName = "Bootstrap Owner Smoke"
    $env:Bootstrap__OwnerToken = "too-short"
    $previousNativePreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $invalidOutput = & dotnet $dllPath --bootstrap-owner 2>&1 | Out-String
    $invalidExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousNativePreference
    if ($invalidExitCode -eq 0 -or $invalidOutput -notmatch "at least 32 characters") { throw "Invalid capability guard failed. Output: $invalidOutput" }
    $absenceSql = "SELECT count(*) FROM `"AspNetUsers`" WHERE `"Email`" = '$primaryEmail';"
    $unexpectedUsers = ($absenceSql | docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U marketplace -d marketplace -At).Trim()
    if ($LASTEXITCODE -ne 0 -or $unexpectedUsers -ne "0") { throw "Invalid bootstrap invocation changed the database." }

    $env:Bootstrap__OwnerToken = "0123456789abcdef0123456789abcdef"

    & dotnet $dllPath --bootstrap-owner | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Initial Owner bootstrap failed." }
    & dotnet $dllPath --bootstrap-owner | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Idempotent Owner bootstrap failed." }
    $verificationSql = @"
SELECT
  (SELECT count(*) FROM "AspNetUsers" u JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id" JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE u."Email" = '$primaryEmail' AND r."Name" = 'Owner'),
  (SELECT count(*) FROM "AuditEvents" a JOIN "AspNetUsers" u ON u."Id" = a."UserId" WHERE u."Email" = '$primaryEmail' AND a."EventType" = 'identity.owner.bootstrapped');
"@
    $verification = ($verificationSql | docker compose exec -T postgres psql -v ON_ERROR_STOP=1 -U marketplace -d marketplace -At -F "|").Trim()
    if ($LASTEXITCODE -ne 0 -or $verification -ne "1|1") { throw "Owner role/audit verification failed: $verification" }

    $env:Bootstrap__OwnerEmail = $secondEmail
    $previousNativePreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $secondOutput = & dotnet $dllPath --bootstrap-owner 2>&1 | Out-String
    $secondExitCode = $LASTEXITCODE
    $ErrorActionPreference = $previousNativePreference
    if ($secondExitCode -eq 0 -or $secondOutput -notmatch "An Owner already exists") { throw "Second Owner guard failed. Output: $secondOutput" }

    Write-Output "Owner bootstrap smoke passed: create, idempotency, audit and second-Owner rejection."
}
finally {
    Remove-SmokeUsers
    $env:ASPNETCORE_ENVIRONMENT = $savedEnvironment.AspNetCore
    $env:ConnectionStrings__Marketplace = $savedEnvironment.Connection
    $env:Bootstrap__OwnerEmail = $savedEnvironment.Email
    $env:Bootstrap__OwnerDisplayName = $savedEnvironment.DisplayName
    $env:Bootstrap__OwnerToken = $savedEnvironment.Token
}
