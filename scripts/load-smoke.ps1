$ErrorActionPreference = "Stop"
$target = "http://127.0.0.1:5080/health/ready"
$started = Get-Date
$jobs = 1..10 | ForEach-Object { Start-Job -ScriptBlock { param($url) 1..10 | ForEach-Object { (Invoke-WebRequest -UseBasicParsing $url -TimeoutSec 10).StatusCode } } -ArgumentList $target }
$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job -Force
$elapsed = (Get-Date) - $started
if ($results.Where({ $_ -ne 200 }).Count -gt 0) { throw "Load smoke received a non-200 response" }
Write-Output "100/100 ready requests passed in $([math]::Round($elapsed.TotalSeconds, 2)) seconds"
