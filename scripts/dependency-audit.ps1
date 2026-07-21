$ErrorActionPreference = "Stop"
$workspacePath = Split-Path -Parent $PSScriptRoot
$projectPath = [System.IO.Path]::Combine($workspacePath, "src", "Marketplace.Web", "Marketplace.Web.csproj")
$uiTestsPath = [System.IO.Path]::Combine($workspacePath, "tests", "UiTests")

function Count-NuGetVulnerabilities($node) {
    if ($null -eq $node -or $node -is [string] -or $node -is [ValueType]) { return 0 }
    if ($node -is [System.Collections.IEnumerable] -and $node -isnot [System.Management.Automation.PSCustomObject]) {
        $count = 0
        foreach ($item in $node) { $count += Count-NuGetVulnerabilities $item }
        return $count
    }

    $count = 0
    foreach ($property in $node.PSObject.Properties) {
        if ($property.Name -eq "vulnerabilities") { $count += @($property.Value).Count }
        else { $count += Count-NuGetVulnerabilities $property.Value }
    }
    return $count
}

$nugetOutput = & dotnet package list --project $projectPath --vulnerable --include-transitive --format json 2>&1
if ($LASTEXITCODE -ne 0) { throw "NuGet vulnerability query failed:`n$($nugetOutput -join [Environment]::NewLine)" }
$nugetResult = ($nugetOutput -join [Environment]::NewLine) | ConvertFrom-Json
$nugetCount = Count-NuGetVulnerabilities $nugetResult
if ($nugetCount -gt 0) { throw "Dependency audit found $nugetCount vulnerable NuGet package entries. Run: dotnet package list --project `"$projectPath`" --vulnerable --include-transitive" }

Push-Location $uiTestsPath
try {
    $npmOutput = & npm audit --json 2>&1
    $npmExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
try { $npmResult = ($npmOutput -join [Environment]::NewLine) | ConvertFrom-Json }
catch { throw "npm vulnerability query returned invalid JSON (exit $npmExitCode)." }
$npmCount = 0
if ($null -ne $npmResult.metadata -and $null -ne $npmResult.metadata.vulnerabilities -and $null -ne $npmResult.metadata.vulnerabilities.total) {
    $npmCount = [int]$npmResult.metadata.vulnerabilities.total
}
if ($npmExitCode -ne 0 -or $npmCount -gt 0) { throw "Dependency audit found $npmCount vulnerable npm package entries. Run npm audit in tests/UiTests for details." }

Write-Output "Dependency audit passed: NuGet and npm report no known vulnerabilities."
