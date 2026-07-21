using System.Security.Claims;

namespace Marketplace.Web.Modules.Identity;

public sealed class DistributedPrivacyExportRateLimitMiddleware(RequestDelegate next, ILogger<DistributedPrivacyExportRateLimitMiddleware> logger)
{
    private const int PermitLimit = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public async Task InvokeAsync(HttpContext context, DistributedRateLimitStore store)
    {
        if (!IsExport(context.Request) || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) { await next(context); return; }
        var now = DateTimeOffset.UtcNow;
        var windowSeconds = (long)Window.TotalSeconds;
        var windowStart = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / windowSeconds * windowSeconds);
        int count;
        try { count = await store.IncrementAsync("privacy-export", userId, windowStart, windowStart + Window + TimeSpan.FromMinutes(1), context.RequestAborted); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Distributed privacy export rate limit storage is unavailable.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "5";
            return;
        }
        if (count > PermitLimit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling((windowStart + Window - now).TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return;
        }
        await next(context);
    }

    private static bool IsExport(HttpRequest request) => HttpMethods.IsGet(request.Method)
        && request.Path.Equals("/Account/Data", StringComparison.OrdinalIgnoreCase)
        && request.Query["handler"].ToString().Equals("Export", StringComparison.OrdinalIgnoreCase);
}
