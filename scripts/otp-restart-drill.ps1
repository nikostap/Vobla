param(
    [string]$BaseUrl = "http://127.0.0.1:5080"
)

$ErrorActionPreference = "Stop"
$drillId = [Guid]::NewGuid().ToString("N")
$tempRoot = [System.IO.Path]::GetTempPath()
$cookie = Join-Path $tempRoot "vobla-otp-$drillId-cookies.txt"
$signin = Join-Path $tempRoot "vobla-otp-$drillId-signin.html"
$issued = Join-Path $tempRoot "vobla-otp-$drillId-issued.html"
$headers = Join-Path $tempRoot "vobla-otp-$drillId-headers.txt"
$response = Join-Path $tempRoot "vobla-otp-$drillId-response.html"
$email = "restart-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())@example.test"

function Read-AntiforgeryToken([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken" type="hidden" value="([^"]+)"')
    if (!$match.Success) { throw "Antiforgery token not found." }
    return $match.Groups[1].Value
}

try {
    & curl.exe -fsS -c $cookie "$BaseUrl/Account/SignIn" -o $signin
    if ($LASTEXITCODE -ne 0) { throw "Sign-in page request failed." }
    $token = Read-AntiforgeryToken (Get-Content $signin -Raw)

    & curl.exe -fsS -b $cookie -c $cookie `
        --data-urlencode "Email=$email" `
        --data-urlencode "__RequestVerificationToken=$token" `
        "$BaseUrl/Account/SignIn?handler=SendCode" -o $issued
    if ($LASTEXITCODE -ne 0) { throw "OTP issue request failed." }
    $issuedHtml = Get-Content $issued -Raw
    $codeMatch = [regex]::Match($issuedHtml, '<strong>([0-9]{6})</strong>')
    if (!$codeMatch.Success) { throw "Development OTP was not rendered." }
    $code = $codeMatch.Groups[1].Value
    $verifyToken = Read-AntiforgeryToken $issuedHtml

    & docker compose restart marketplace-web | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Application restart failed." }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(45)
    $ErrorActionPreference = "Continue"
    do {
        Start-Sleep -Milliseconds 500
        & curl.exe -fsS "$BaseUrl/health/ready" -o NUL 2>$null
        $ready = $LASTEXITCODE -eq 0
    } until ($ready -or [DateTimeOffset]::UtcNow -gt $deadline)
    $ErrorActionPreference = "Stop"
    if (!$ready) { throw "Application did not become ready after restart." }

    & curl.exe -sS -D $headers -o $response -b $cookie `
        --data-urlencode "Email=$email" `
        --data-urlencode "Code=$code" `
        --data-urlencode "__RequestVerificationToken=$verifyToken" `
        "$BaseUrl/Account/SignIn?handler=Verify"
    if ($LASTEXITCODE -ne 0) { throw "OTP verification request failed." }
    $headerText = Get-Content $headers -Raw
    if ($headerText -notmatch 'HTTP/1\.1 302' -or $headerText -notmatch 'Location: /Account/Profile') {
        throw "OTP verification after restart did not redirect to the profile.`n$headerText"
    }

    Write-Host "OTP restart drill passed for $email."
}
finally {
    foreach ($path in @($cookie, $signin, $issued, $headers, $response)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
}
