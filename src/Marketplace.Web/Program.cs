using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Marketplace.Web.Modules.Geo;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Marketplace.Web.Modules.Observability;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Marketplace.Web.Modules.Storage;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
var bootstrapOwnerRequested = args.Contains("--bootstrap-owner", StringComparer.OrdinalIgnoreCase);
const long DefaultRequestBodyLimit = 1 * 1024 * 1024;
const long ChatRequestBodyLimit = 65 * 1024 * 1024;
const long ListingRequestBodyLimit = 105 * 1024 * 1024;
long RequestBodyLimitFor(PathString path) => path.StartsWithSegments("/Messages/Chat", StringComparison.OrdinalIgnoreCase)
    ? ChatRequestBodyLimit
    : path.StartsWithSegments("/Listings/Create", StringComparison.OrdinalIgnoreCase)
        ? ListingRequestBodyLimit
        : DefaultRequestBodyLimit;
bool IsPublicCompressionPath(PathString path) => path == "/" || path.StartsWithSegments("/Catalog", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/Components", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
bool IsSensitiveNoStorePath(PathString path) =>
    path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Messages", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Favorites", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Compare", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Recommendations", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Monetization", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Listings/Create", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/Listings/My", StringComparison.OrdinalIgnoreCase);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = ListingRequestBodyLimit;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.MaxRequestHeaderCount = 100;
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    options.Limits.MaxRequestLineSize = 8 * 1024;
});
var knownProxyValues = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
var knownProxies = new List<IPAddress>();
foreach (var value in knownProxyValues)
{
    if (IPAddress.TryParse(value, out var address)) knownProxies.Add(address);
    else if (builder.Environment.IsProduction()) throw new InvalidOperationException($"Production configuration validation failed: Reverse proxy address '{value}' is invalid.");
}

if (builder.Environment.IsProduction())
{
    var configurationErrors = new List<string>();
    if (builder.Configuration["AllowedHosts"] is null or "*" || builder.Configuration["AllowedHosts"]!.Contains("localhost", StringComparison.OrdinalIgnoreCase)) configurationErrors.Add("AllowedHosts must contain explicit public hosts.");
    if (builder.Configuration.GetValue("Beta:RegistrationOpen", true)) configurationErrors.Add("Beta registration must be closed in Production.");
    var inviteCode = builder.Configuration["Beta:InviteCode"];
    if (string.IsNullOrWhiteSpace(inviteCode) || inviteCode.Length < 16 || inviteCode == "local-beta") configurationErrors.Add("Beta invite code must be a secret of at least 16 characters.");
    var productionConnection = builder.Configuration.GetConnectionString("Marketplace") ?? string.Empty;
    if (productionConnection.Contains("marketplace_dev", StringComparison.OrdinalIgnoreCase) || productionConnection.Contains("localhost", StringComparison.OrdinalIgnoreCase)) configurationErrors.Add("Production database connection must not use local demo credentials.");
    var metricsToken = builder.Configuration["Observability:MetricsToken"];
    if (string.IsNullOrWhiteSpace(metricsToken) || metricsToken.Length < 24 || metricsToken == "local-metrics") configurationErrors.Add("Observability metrics token must be a secret of at least 24 characters.");
    if (!string.IsNullOrWhiteSpace(builder.Configuration["Identity:DemoAdminEmail"])) configurationErrors.Add("Automatic demo administrator elevation must be disabled in Production.");
    if (builder.Configuration["Identity:OtpProvider"]?.Equals("Fake", StringComparison.OrdinalIgnoreCase) != false) configurationErrors.Add("Fake OTP provider must be replaced in Production.");
    if (knownProxies.Count == 0) configurationErrors.Add("At least one explicit reverse proxy IP address is required in Production.");
    if (!bootstrapOwnerRequested && !string.IsNullOrWhiteSpace(builder.Configuration["Bootstrap:OwnerToken"])) configurationErrors.Add("Bootstrap Owner token must not remain configured during normal web startup.");
    if (configurationErrors.Count > 0) throw new InvalidOperationException("Production configuration validation failed: " + string.Join(" ", configurationErrors));
}

builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsProduction() ? "__Host-marketplace.csrf" : "marketplace.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = ListingRequestBodyLimit;
    options.ValueCountLimit = 2_048;
    options.KeyLengthLimit = 2_048;
    options.ValueLengthLimit = 64 * 1024;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = knownProxies.Count == 0
        ? ForwardedHeaders.None
        : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var address in knownProxies) options.KnownProxies.Add(address);
});
builder.Services.AddSignalR();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("database", timeout: TimeSpan.FromSeconds(3), tags: ["ready"]);
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        var handler = context.HttpContext.Request.Query["handler"].ToString();
        var action = context.HttpContext.Request.Method == HttpMethods.Get ? "page" : handler.Equals("SendCode", StringComparison.OrdinalIgnoreCase) ? "send" : "verify";
        context.HttpContext.RequestServices.GetService<RuntimeMetrics>()?.AuthenticationEvent(action, "rate_limited");
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("auth", context =>
    {
        var handler = context.Request.Query["handler"].ToString();
        var action = context.Request.Method == HttpMethods.Get ? "page" : handler.Equals("SendCode", StringComparison.OrdinalIgnoreCase) ? "send" : "verify";
        var permitLimit = action switch { "send" => 60, "verify" => 40, _ => 120 };
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{ip}:{action}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("geocoder", context => RateLimitPartition.GetFixedWindowLimiter(
        $"geo:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddMarketplaceMaps(builder.Configuration);
builder.Services.AddSingleton<JsonListingCatalog>();
builder.Services.AddSingleton<RuntimeMetrics>();
builder.Services.AddScoped<IListingCatalog, DatabaseListingCatalog>();
builder.Services.AddSingleton<IIpGeoAdapter, LocalIpGeoAdapter>();
builder.Services.AddSingleton<IMapProviderAdapter, DemoYandexMapAdapter>();
builder.Services.AddScoped<Marketplace.Web.Modules.Messaging.MessagingService>();
builder.Services.AddScoped<DistributedRateLimitStore>();
builder.Services.AddScoped<Marketplace.Web.Modules.Monetization.QuotaService>();
builder.Services.AddScoped<Marketplace.Web.Modules.Monetization.SandboxPaymentService>();
builder.Services.AddHostedService<Marketplace.Web.Modules.Administration.MaintenanceJobWorker>();
var otpProvider = builder.Configuration["Identity:OtpProvider"] ?? "Fake";
if (!otpProvider.Equals("Fake", StringComparison.OrdinalIgnoreCase) && !bootstrapOwnerRequested) throw new InvalidOperationException($"OTP provider '{otpProvider}' is not registered in this build.");
builder.Services.AddScoped<IOneTimeCodeService, FakeEmailCodeService>();
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Marketplace"), npgsql => npgsql.CommandTimeout(15)));
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = true;
    })
    .AddEntityFrameworkStores<MarketplaceDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/SignIn";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = builder.Environment.IsProduction() ? "__Host-marketplace.session" : "marketplace.session";
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            var rawSessionId = context.Principal?.FindFirst("session_id")?.Value;
            if (!Guid.TryParse(rawSessionId, out var sessionId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<MarketplaceDbContext>();
            var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.RevokedAt == null);
            if (session is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            var now = DateTimeOffset.UtcNow;
            if (session.CreatedAt < now.AddDays(-30))
            {
                session.RevokedAt = now;
                await db.SaveChangesAsync(context.HttpContext.RequestAborted);
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                return;
            }

            if (session.LastSeenAt < now.AddMinutes(-5))
            {
                session.LastSeenAt = now;
                await db.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
        }
    };
});

var app = builder.Build();

if (bootstrapOwnerRequested)
{
    BootstrapOwnerCommand.ValidateConfiguration(app.Configuration);
    await IdentitySeed.InitializeAsync(app.Services);
    await BootstrapOwnerCommand.ExecuteAsync(app.Services, app.Configuration);
    app.Logger.LogInformation("Initial Owner provisioning command completed successfully.");
    return;
}

if (args.Contains("--seed-only", StringComparer.OrdinalIgnoreCase))
{
    await IdentitySeed.InitializeAsync(app.Services);
    app.Logger.LogInformation("Database migrations and idempotent seed completed.");
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseWhen(context => HttpMethods.IsGet(context.Request.Method) && IsPublicCompressionPath(context.Request.Path), branch => branch.UseResponseCompression());
app.Use(async (context, next) =>
{
    var incomingCorrelationId = context.Request.Headers["X-Correlation-ID"].ToString().Trim();
    var correlationId = Guid.TryParse(incomingCorrelationId, out _) ? incomingCorrelationId : Guid.NewGuid().ToString("D");
    context.TraceIdentifier = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), payment=(), usb=()";
        context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data: blob:; connect-src 'self' ws: wss:; worker-src 'self' blob:; child-src blob:; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        if (context.User.Identity?.IsAuthenticated == true || IsSensitiveNoStorePath(context.Request.Path))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }
        return Task.CompletedTask;
    });
    var metrics = context.RequestServices.GetRequiredService<RuntimeMetrics>();
    metrics.RequestStarted();
    var startedAt = Stopwatch.GetTimestamp();
    var statusCode = StatusCodes.Status500InternalServerError;
    using (app.Logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
    {
        try
        {
            var requestLimit = RequestBodyLimitFor(context.Request.Path);
            if (context.Request.ContentLength > requestLimit)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            }
            else
            {
                await next();
            }
            statusCode = context.Response.StatusCode;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            metrics.RequestCompleted(statusCode, elapsed);
            app.Logger.LogInformation(
                "HTTP {Method} {Path} returned {StatusCode} in {ElapsedMs:F1} ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                elapsed.TotalMilliseconds);
        }
    }
});

var uploadsRoot = Path.Combine(app.Environment.WebRootPath, "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads",
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable"
});

app.UseRouting();
app.UseMiddleware<DistributedAuthRateLimitMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<DistributedPrivacyExportRateLimitMiddleware>();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapGet("/metrics", (HttpContext context, RuntimeMetrics metrics) =>
{
    if (app.Environment.IsProduction())
    {
        var expectedToken = app.Configuration["Observability:MetricsToken"];
        var suppliedToken = context.Request.Headers.Authorization.ToString();
        var expectedBytes = Encoding.UTF8.GetBytes($"Bearer {expectedToken}");
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        if (string.IsNullOrEmpty(expectedToken) || !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes)) return Results.Unauthorized();
    }
    return Results.Text(metrics.RenderPrometheus(), "text/plain; version=0.0.4; charset=utf-8");
});
if (app.Environment.IsDevelopment())
{
    app.MapGet("/health/probes/request-context", (HttpContext context) => Results.Ok(new { remoteIp = context.Connection.RemoteIpAddress?.ToString(), context.Request.Scheme }));
    app.MapGet("/health/probes/request-limit", (string path) => Results.Ok(new { path, bytes = RequestBodyLimitFor(new PathString(path)) }));
    app.MapGet("/health/probes/upload-temp-cleanup", (IWebHostEnvironment environment) =>
    {
        var probeRoot = Path.Combine(environment.ContentRootPath, "App_Data", "chat", $"cleanup-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(probeRoot);
        var stale = Path.Combine(probeRoot, ".stale.txt.uploading");
        var fresh = Path.Combine(probeRoot, ".fresh.txt.uploading");
        var completed = Path.Combine(probeRoot, "completed.txt");
        try
        {
            File.WriteAllText(stale, "stale"); File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-2));
            File.WriteAllText(fresh, "fresh"); File.WriteAllText(completed, "completed");
            var removed = UploadTemporaryFileCleanup.RemoveOlderThan([probeRoot], DateTimeOffset.UtcNow.AddHours(-1));
            return Results.Ok(new { removed, staleExists = File.Exists(stale), freshExists = File.Exists(fresh), completedExists = File.Exists(completed) });
        }
        finally { if (Directory.Exists(probeRoot)) Directory.Delete(probeRoot, true); }
    });
    app.MapGet("/health/probes/moderation", () =>
    {
        var started = Stopwatch.GetTimestamp(); var critical = 0;
        for (var index = 0; index < 10_000; index++)
        {
            var listing = new Marketplace.Web.Modules.Listings.Listing { Id = Guid.NewGuid(), Title = index % 10 == 0 ? "Запрещённый тестовый товар" : $"Безопасное объявление {index}", Description = index % 10 == 0 ? "оружие" : "Обычное описание товара", DealType = "FixedPrice" };
            if (Marketplace.Web.Modules.Moderation.DemoModerationEngine.Evaluate(listing).RiskLevel == "Critical") critical++;
        }
        return Results.Ok(new { evaluated = 10_000, critical, elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds });
    });
}
app.MapHub<Marketplace.Web.Modules.Messaging.ChatHub>("/hubs/chat");
app.MapMarketplaceMaps();
app.MapRazorPages()
   .WithStaticAssets();

await IdentitySeed.InitializeAsync(app.Services);

app.Run();
