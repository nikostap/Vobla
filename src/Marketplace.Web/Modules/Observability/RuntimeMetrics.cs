using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Marketplace.Web.Modules.Observability;

public sealed class RuntimeMetrics
{
    private long _activeRequests;
    private long _httpRequests;
    private long _httpDurationMicroseconds;
    private long _lastBackgroundJobCompletedUnixSeconds;
    private readonly ConcurrentDictionary<int, long> _httpStatusFamilies = new();
    private readonly ConcurrentDictionary<(string JobName, string Status), long> _backgroundJobs = new();
    private readonly ConcurrentDictionary<(string Action, string Outcome), long> _authenticationEvents = new();

    public void RequestStarted() => Interlocked.Increment(ref _activeRequests);

    public void RequestCompleted(int statusCode, TimeSpan elapsed)
    {
        Interlocked.Increment(ref _httpRequests);
        Interlocked.Add(ref _httpDurationMicroseconds, (long)(elapsed.TotalMilliseconds * 1_000));
        _httpStatusFamilies.AddOrUpdate(Math.Clamp(statusCode / 100, 1, 5), 1, (_, value) => value + 1);
        Interlocked.Decrement(ref _activeRequests);
    }

    public void BackgroundJobCompleted(string jobName, string status)
    {
        _backgroundJobs.AddOrUpdate((jobName, status), 1, (_, value) => value + 1);
        if (status is "Completed" or "Failed")
            Interlocked.Exchange(ref _lastBackgroundJobCompletedUnixSeconds, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public void AuthenticationEvent(string action, string outcome) =>
        _authenticationEvents.AddOrUpdate((NormalizeAction(action), NormalizeOutcome(outcome)), 1, (_, value) => value + 1);

    public string RenderPrometheus()
    {
        var result = new StringBuilder();
        result.AppendLine("# HELP marketplace_http_requests_total Total HTTP requests completed by this instance.");
        result.AppendLine("# TYPE marketplace_http_requests_total counter");
        result.Append("marketplace_http_requests_total ").AppendLine(Interlocked.Read(ref _httpRequests).ToString(CultureInfo.InvariantCulture));
        result.AppendLine("# HELP marketplace_http_active_requests Current HTTP requests being processed by this instance.");
        result.AppendLine("# TYPE marketplace_http_active_requests gauge");
        result.Append("marketplace_http_active_requests ").AppendLine(Interlocked.Read(ref _activeRequests).ToString(CultureInfo.InvariantCulture));
        result.AppendLine("# HELP marketplace_http_request_duration_seconds_sum Cumulative HTTP request duration.");
        result.AppendLine("# TYPE marketplace_http_request_duration_seconds_sum counter");
        result.Append("marketplace_http_request_duration_seconds_sum ").AppendLine((Interlocked.Read(ref _httpDurationMicroseconds) / 1_000_000d).ToString("0.######", CultureInfo.InvariantCulture));
        result.AppendLine("# HELP marketplace_http_responses_total HTTP responses grouped by status class.");
        result.AppendLine("# TYPE marketplace_http_responses_total counter");
        foreach (var item in _httpStatusFamilies.OrderBy(x => x.Key))
            result.Append("marketplace_http_responses_total{status_class=\"").Append(item.Key).Append("xx\"} ").AppendLine(item.Value.ToString(CultureInfo.InvariantCulture));
        result.AppendLine("# HELP marketplace_background_jobs_total Maintenance job attempts grouped by bounded job name and outcome.");
        result.AppendLine("# TYPE marketplace_background_jobs_total counter");
        foreach (var item in _backgroundJobs.OrderBy(x => x.Key.JobName).ThenBy(x => x.Key.Status))
            result.Append("marketplace_background_jobs_total{job=\"").Append(EscapeLabel(item.Key.JobName)).Append("\",status=\"").Append(EscapeLabel(item.Key.Status.ToLowerInvariant())).Append("\"} ").AppendLine(item.Value.ToString(CultureInfo.InvariantCulture));
        result.AppendLine("# HELP marketplace_background_job_last_completion_timestamp_seconds Unix timestamp of the latest terminal maintenance job result.");
        result.AppendLine("# TYPE marketplace_background_job_last_completion_timestamp_seconds gauge");
        result.Append("marketplace_background_job_last_completion_timestamp_seconds ").AppendLine(Interlocked.Read(ref _lastBackgroundJobCompletedUnixSeconds).ToString(CultureInfo.InvariantCulture));
        result.AppendLine("# HELP marketplace_auth_events_total Authentication events grouped by bounded action and outcome.");
        result.AppendLine("# TYPE marketplace_auth_events_total counter");
        foreach (var item in _authenticationEvents.OrderBy(x => x.Key.Action).ThenBy(x => x.Key.Outcome))
            result.Append("marketplace_auth_events_total{action=\"").Append(item.Key.Action).Append("\",outcome=\"").Append(item.Key.Outcome).Append("\"} ").AppendLine(item.Value.ToString(CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static string NormalizeAction(string value) => value is "page" or "send" or "verify" ? value : "other";
    private static string NormalizeOutcome(string value) => value is "success" or "failed" or "rate_limited" ? value : "other";
    private static string EscapeLabel(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}
