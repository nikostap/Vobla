using Marketplace.Web.Modules.Observability;

namespace Marketplace.Web.Modules.Identity;

public sealed class DistributedAuthRateLimitMiddleware(RequestDelegate next, ILogger<DistributedAuthRateLimitMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, DistributedRateLimitStore store, RuntimeMetrics metrics)
    {
        if (!context.Request.Path.StartsWithSegments("/Account/SignIn", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var action = Action(context.Request);
        var limit = action switch { "send" => 60, "verify" => 40, _ => 120 };
        var now = DateTimeOffset.UtcNow;
        var windowStart = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds() / 60 * 60);

        int count;
        try
        {
            count = await store.IncrementAsync("authentication", $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}|{action}", windowStart, windowStart.AddMinutes(2), context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Distributed authentication rate limit storage is unavailable.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "5";
            return;
        }

        if (count > limit)
        {
            metrics.AuthenticationEvent(action, "rate_limited");
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            return;
        }

        await next(context);
    }

    private static string Action(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method)) return "page";
        var handler = request.Query["handler"].ToString();
        return handler.Equals("SendCode", StringComparison.OrdinalIgnoreCase) ? "send" : "verify";
    }

}
