$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$workspacePath = Split-Path -Parent $PSScriptRoot
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(7)
$databaseRecovered = $false
$applicationRecovered = $false

Push-Location $workspacePath
try {
    docker compose stop postgres | Out-Null
    Start-Sleep -Seconds 2
    $liveWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $liveResponse = $client.GetAsync("http://127.0.0.1:5080/health/live").GetAwaiter().GetResult()
    $liveWatch.Stop()
    if ([int]$liveResponse.StatusCode -ne 200) { throw "Liveness returned $([int]$liveResponse.StatusCode), expected 200 while PostgreSQL was stopped." }
    if ($liveWatch.Elapsed -gt [TimeSpan]::FromSeconds(2)) { throw "Liveness response took $($liveWatch.Elapsed.TotalSeconds.ToString('F2')) seconds, expected at most 2." }
    Write-Output "Liveness remained 200 in $($liveWatch.Elapsed.TotalSeconds.ToString('F2')) seconds while PostgreSQL was unavailable."
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $response = $client.GetAsync("http://127.0.0.1:5080/health/ready").GetAwaiter().GetResult()
    $watch.Stop()
    if ([int]$response.StatusCode -ne 503) { throw "Readiness returned $([int]$response.StatusCode), expected 503 while PostgreSQL was stopped." }
    if ($watch.Elapsed -gt [TimeSpan]::FromSeconds(6)) { throw "Readiness failure response took $($watch.Elapsed.TotalSeconds.ToString('F2')) seconds, expected at most 6." }
    Write-Output "Readiness returned 503 in $($watch.Elapsed.TotalSeconds.ToString('F2')) seconds while PostgreSQL was unavailable."
}
finally {
    docker compose start postgres | Out-Null
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    do {
        $databaseHealth = docker inspect --format '{{.State.Health.Status}}' vobla-postgres-1 2>$null
        $applicationHealth = docker inspect --format '{{.State.Health.Status}}' vobla-marketplace-web-1 2>$null
        $databaseRecovered = $databaseHealth -eq "healthy"
        $applicationRecovered = $applicationHealth -eq "healthy"
        if ($databaseRecovered -and $applicationRecovered) { break }
        Start-Sleep -Seconds 2
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    $client.Dispose()
    Pop-Location
}

if (-not $databaseRecovered -or -not $applicationRecovered) { throw "Stack did not recover within 60 seconds: database=$databaseHealth application=$applicationHealth." }
Write-Output "PostgreSQL and application health checks recovered."
