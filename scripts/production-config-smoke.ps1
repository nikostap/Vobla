$ErrorActionPreference = "Stop"
$workspacePath = Split-Path -Parent $PSScriptRoot
$dllPath = [System.IO.Path]::Combine($workspacePath, "src", "Marketplace.Web", "bin", "Release", "net10.0", "Marketplace.Web.dll")
if (-not (Test-Path -LiteralPath $dllPath)) { dotnet build ([System.IO.Path]::Combine($workspacePath, "Marketplace.slnx")) -c Release | Out-Null }
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $ErrorActionPreference = "Continue"
    $output = & dotnet $dllPath 2>&1 | Out-String
    $processExitCode = $LASTEXITCODE
    $ErrorActionPreference = "Stop"
    if ($processExitCode -eq 0) { throw "Application unexpectedly accepted unsafe production defaults." }
    if ($output -notmatch "Production configuration validation failed") { throw "Expected validation error was not emitted. Output: $output" }
    Write-Output "Unsafe production defaults were rejected as expected."
}
finally { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment }
