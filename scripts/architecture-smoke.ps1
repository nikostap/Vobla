$ErrorActionPreference = "Stop"
$workspacePath = Split-Path -Parent $PSScriptRoot
$sourcePath = [System.IO.Path]::Combine($workspacePath, "src", "Marketplace.Web")
$errors = [System.Collections.Generic.List[string]]::new()

function Require-OrderedTokens([string]$text, [string[]]$tokens, [string]$description) {
    $position = -1
    foreach ($token in $tokens) {
        $next = $text.IndexOf($token, $position + 1, [StringComparison]::Ordinal)
        if ($next -lt 0) { $errors.Add("$description is missing '$token'."); return }
        if ($next -le $position) { $errors.Add("$description has '$token' in the wrong order."); return }
        $position = $next
    }
}

$program = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Program.cs")) -Raw
$workflow = Get-Content -LiteralPath ([System.IO.Path]::Combine($workspacePath, ".github", "workflows", "ci.yml")) -Raw
Require-OrderedTokens $program @("app.UseForwardedHeaders()", "app.UseMiddleware<DistributedAuthRateLimitMiddleware>()", "app.UseRateLimiter()", "app.UseAuthentication()", "app.UseMiddleware<DistributedPrivacyExportRateLimitMiddleware>()", "app.UseAuthorization()") "Security middleware pipeline"
foreach ($required in @("Automatic demo administrator elevation must be disabled in Production.", "Fake OTP provider must be replaced in Production.", "At least one explicit reverse proxy IP address is required in Production.", "Observability metrics token must be a secret")) {
    if ($program.IndexOf($required, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Production fail-fast guard is missing: $required") }
}
$bootstrapCommand = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Identity", "BootstrapOwnerCommand.cs")) -Raw
foreach ($bootstrapInvariant in @("Bootstrap:OwnerToken", "pg_advisory_xact_lock", 'GetUsersInRoleAsync("Owner")', "identity.owner.bootstrapped", "CommitAsync")) {
    if ($bootstrapCommand.IndexOf($bootstrapInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Owner bootstrap invariant is missing: $bootstrapInvariant") }
}
foreach ($bootstrapProgramInvariant in @("--bootstrap-owner", "Bootstrap Owner token must not remain configured during normal web startup.", "BootstrapOwnerCommand.ValidateConfiguration", "BootstrapOwnerCommand.ExecuteAsync")) {
    if ($program.IndexOf($bootstrapProgramInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Owner bootstrap entry point is missing: $bootstrapProgramInvariant") }
}
foreach ($cookieInvariant in @("__Host-marketplace.session", "__Host-marketplace.csrf", "CookieSecurePolicy.Always", "SameSiteMode.Strict")) {
    if ($program.IndexOf($cookieInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Cookie security invariant is missing: $cookieInvariant") }
}
foreach ($responseInvariant in @("X-Frame-Options", "Cross-Origin-Opener-Policy", "Cross-Origin-Resource-Policy", "no-store, no-cache", "IsSensitiveNoStorePath")) {
    if ($program.IndexOf($responseInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Private response security invariant is missing: $responseInvariant") }
}
if ($workflow.IndexOf("./scripts/dependency-audit.ps1", [StringComparison]::Ordinal) -lt 0) {
    $errors.Add("CI dependency vulnerability gate is missing.")
}
if ($program.IndexOf("style-src 'self';", [StringComparison]::Ordinal) -lt 0 -or $program.IndexOf("style-src 'self' 'unsafe-inline'", [StringComparison]::Ordinal) -ge 0) {
    $errors.Add("CSP must restrict styles to same-origin files without unsafe-inline.")
}

$adminPath = [System.IO.Path]::Combine($sourcePath, "Pages", "Admin")
foreach ($file in Get-ChildItem -LiteralPath $adminPath -Filter "*.cshtml.cs" -File) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -notmatch '\[Authorize\s*\(\s*Roles\s*=') { $errors.Add("Admin PageModel lacks explicit role authorization: $($file.Name)") }
}

$razorFiles = Get-ChildItem -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages")) -Recurse -Filter "*.cshtml" -File
foreach ($file in $razorFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match '<script(?![^>]*\bsrc\s*=)[^>]*>') { $errors.Add("Inline script is forbidden by CSP architecture: $($file.FullName)") }
    if ($content -match '\sstyle\s*=') { $errors.Add("Inline style attribute is forbidden by CSP architecture: $($file.FullName)") }
    if ($content -match '<(?:script|link)[^>]+(?:src|href)\s*=\s*["'']https?://') { $errors.Add("Remote script/style dependency is forbidden: $($file.FullName)") }
    if ($content -match 'Html\.Raw\s*\(') { $errors.Add("Unreviewed Html.Raw usage: $($file.FullName)") }
}

foreach ($legalPage in @("Privacy.cshtml", "Terms.cshtml", "Cookies.cshtml", "Rules.cshtml")) {
    $legalPath = [System.IO.Path]::Combine($sourcePath, "Pages", $legalPage)
    if (-not [System.IO.File]::Exists($legalPath)) { $errors.Add("Required pre-release legal notice is missing: $legalPage") }
}
$privacyNotice = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Privacy.cshtml")) -Raw
if ($privacyNotice.IndexOf("Use this page to detail", [StringComparison]::OrdinalIgnoreCase) -ge 0) { $errors.Add("Default Privacy template placeholder must not be published.") }
$layout = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Shared", "_Layout.cshtml")) -Raw
foreach ($legalLink in @("/Privacy", "/Terms", "/Cookies", "/Rules")) {
    if ($layout.IndexOf($legalLink, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Global legal footer link is missing: $legalLink") }
}
$errorPage = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Error.cshtml")) -Raw
foreach ($unsafeErrorText in @("Development Mode", "Exception.Message", "StackTrace")) {
    if ($errorPage.IndexOf($unsafeErrorText, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $errors.Add("Error page exposes unsafe diagnostic text: $unsafeErrorText") }
}
$errorModel = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Error.cshtml.cs")) -Raw
foreach ($errorInvariant in @("Status500InternalServerError", "HttpContext.TraceIdentifier")) {
    if ($errorModel.IndexOf($errorInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Error response invariant is missing: $errorInvariant") }
}
$dataExport = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Account", "Data.cshtml.cs")) -Raw
foreach ($exportInvariant in @("[Authorize]", "AsNoTracking()", "SerializeToUtf8Bytes", "application/json; charset=utf-8")) {
    if ($dataExport.IndexOf($exportInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Account data export invariant is missing: $exportInvariant") }
}
$homePage = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Index.cshtml")) -Raw
foreach ($homeNavigation in @('asp-page="/Favorites"', 'asp-page="/Messages/Index"', 'asp-page="/Listings/Create"', 'asp-page="/Catalog"')) {
    if ($homePage.IndexOf($homeNavigation, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Homepage product navigation is missing: $homeNavigation") }
}
if ($homePage.IndexOf('asp-route-category="@category.Slug"', [StringComparison]::Ordinal) -lt 0) { $errors.Add("Homepage category chips must filter listings instead of opening the catalog editor/directory.") }
$databaseCatalog = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Catalog", "DatabaseListingCatalog.cs")) -Raw
foreach ($categorySearchInvariant in @("categoryTree", "ParentId", "categoryIds.Contains(x.CategoryId)")) {
    if ($databaseCatalog.IndexOf($categorySearchInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Hierarchical category search invariant is missing: $categorySearchInvariant") }
}
$developmentSeed = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Listings", "DevelopmentListingSeed.cs")) -Raw
$identitySeed = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Identity", "IdentitySeed.cs")) -Raw
foreach ($featuredInvariant in @("environment.IsDevelopment()", "DevelopmentListingSeed.InitializeAsync")) {
    if ($identitySeed.IndexOf($featuredInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Development featured listing registration is missing: $featuredInvariant") }
}
foreach ($featuredInvariant in @('Status = "Active"', "CategorySchemaVersionId", "ListingLocation")) {
    if ($developmentSeed.IndexOf($featuredInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Development featured listing invariant is missing: $featuredInvariant") }
}
if ($homePage.IndexOf("components-link", [StringComparison]::Ordinal) -ge 0) { $errors.Add("Internal component gallery must not appear in the public homepage navigation.") }
$signInModel = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Account", "SignIn.cshtml.cs")) -Raw
foreach ($returnInvariant in @("Url.IsLocalUrl(ReturnUrl)", "LocalRedirect(ReturnUrl)")) {
    if ($signInModel.IndexOf($returnInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Post-sign-in local return invariant is missing: $returnInvariant") }
}
foreach ($secretField in @("PasswordHash", "SecurityStamp", "CodeHash", "DebugCode", ".StorageKey")) {
    if ($dataExport.IndexOf($secretField, [StringComparison]::Ordinal) -ge 0) { $errors.Add("Account data export must not expose secret/internal field: $secretField") }
}
foreach ($erasureInvariant in @("OnPostRequestErasureAsync", "OnPostCancelErasureAsync", "privacy.erasure.requested", "privacy.erasure.cancelled")) {
    if ($dataExport.IndexOf($erasureInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Account erasure request invariant is missing: $erasureInvariant") }
}
foreach ($transitionInvariant in @("BeginTransactionAsync", "ExecuteUpdateAsync", "updated != 1", "CommitAsync")) {
    if ($dataExport.IndexOf($transitionInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Atomic erasure cancellation invariant is missing: $transitionInvariant") }
}
$privacyAdmin = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Pages", "Admin", "Privacy.cshtml.cs")) -Raw
foreach ($transitionInvariant in @("BeginTransactionAsync", "ExecuteUpdateAsync", "updated != 1", "CommitAsync")) {
    if ($privacyAdmin.IndexOf($transitionInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Atomic erasure decision invariant is missing: $transitionInvariant") }
}
foreach ($automaticErasure in @("ExecuteDelete", "Remove(identityUser)", "DeleteAsync(identityUser)")) {
    if ($dataExport.IndexOf($automaticErasure, [StringComparison]::Ordinal) -ge 0) { $errors.Add("Account erasure must not bypass the approved retention workflow: $automaticErasure") }
}
$dbContext = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Identity", "MarketplaceDbContext.cs")) -Raw
if ($dbContext.IndexOf('HasFilter("\"Status\" = ''Pending''")', [StringComparison]::Ordinal) -lt 0) { $errors.Add("Pending account erasure requests need a database uniqueness guard.") }
$privacyLimiter = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Identity", "DistributedPrivacyExportRateLimitMiddleware.cs")) -Raw
foreach ($privacyLimitInvariant in @("privacy-export", "PermitLimit = 5", "TimeSpan.FromMinutes(15)", "Status429TooManyRequests", "Status503ServiceUnavailable")) {
    if ($privacyLimiter.IndexOf($privacyLimitInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Privacy export rate-limit invariant is missing: $privacyLimitInvariant") }
}

$codeFiles = Get-ChildItem -LiteralPath $sourcePath -Recurse -Filter "*.cs" -File | Where-Object { $_.FullName -notmatch '[\\/]Migrations[\\/]' -and $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
foreach ($file in $codeFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw
    if ($content -match '\.Result\b|\.Wait\s*\(') { $errors.Add("Sync-over-async is forbidden: $($file.FullName)") }
    if ($content -match 'admin@marketplace\.local') { $errors.Add("Hardcoded demo administrator email is forbidden in C#: $($file.FullName)") }
}

foreach ($relativePath in @([System.IO.Path]::Combine("Pages", "Messages", "Chat.cshtml.cs"), [System.IO.Path]::Combine("Pages", "Listings", "Create.cshtml.cs"))) {
    $uploadHandler = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, $relativePath)) -Raw
    if ($uploadHandler -notmatch 'AtomicUploadStorage\.SaveAsync') { $errors.Add("Upload handler bypasses atomic storage: $relativePath") }
    if ($uploadHandler -match 'System\.IO\.File\.Create') { $errors.Add("Upload handler writes directly instead of temp+rename: $relativePath") }
}

$chatHub = Get-Content -LiteralPath ([System.IO.Path]::Combine($sourcePath, "Modules", "Messaging", "ChatHub.cs")) -Raw
foreach ($realtimeInvariant in @("Context.ConnectionAborted.IsCancellationRequested", "ICollection<KeyValuePair<Guid, int>>")) {
    if ($chatHub.IndexOf($realtimeInvariant, [StringComparison]::Ordinal) -lt 0) { $errors.Add("Realtime lifecycle invariant is missing: $realtimeInvariant") }
}

if ($errors.Count -gt 0) { throw "Architecture smoke failed:`n - " + ($errors -join "`n - ") }
Write-Output "Architecture smoke passed: middleware order, admin authorization, strict CSP dependencies and async rules are intact."
