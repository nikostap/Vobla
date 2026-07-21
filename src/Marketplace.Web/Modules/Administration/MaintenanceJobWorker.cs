using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Engagement;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Recommendations;
using Microsoft.EntityFrameworkCore;
using Marketplace.Web.Modules.Observability;
using Marketplace.Web.Modules.Storage;

namespace Marketplace.Web.Modules.Administration;

public sealed class MaintenanceJobWorker(IServiceScopeFactory scopeFactory, ILogger<MaintenanceJobWorker> logger, RuntimeMetrics metrics, IWebHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await EnqueueScheduledAsync(stoppingToken); while (await ProcessNextAsync(stoppingToken)) { } }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Maintenance worker iteration failed."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task EnqueueScheduledAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(); var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // One scheduler per PostgreSQL database may evaluate recurring jobs at a time.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(764218)", cancellationToken);
        if (!await db.BackgroundJobRuns.AnyAsync(x => x.JobName == "history-retention" && x.StartedAt > now.AddHours(-24) && x.Status != "Failed", cancellationToken)) db.BackgroundJobRuns.Add(NewRun("history-retention"));
        if (!await db.BackgroundJobRuns.AnyAsync(x => x.JobName == "saved-search-matches" && x.StartedAt > now.AddMinutes(-15) && x.Status != "Failed", cancellationToken)) db.BackgroundJobRuns.Add(NewRun("saved-search-matches"));
        if (!await db.BackgroundJobRuns.AnyAsync(x => x.JobName == "upload-temp-cleanup" && x.StartedAt > now.AddHours(-1) && x.Status != "Failed", cancellationToken)) db.BackgroundJobRuns.Add(NewRun("upload-temp-cleanup"));
        if (!await db.BackgroundJobRuns.AnyAsync(x => x.JobName == "otp-challenge-cleanup" && x.StartedAt > now.AddHours(-1) && x.Status != "Failed", cancellationToken)) db.BackgroundJobRuns.Add(NewRun("otp-challenge-cleanup"));
        if (!await db.BackgroundJobRuns.AnyAsync(x => x.JobName == "auth-rate-limit-cleanup" && x.StartedAt > now.AddHours(-1) && x.Status != "Failed", cancellationToken)) db.BackgroundJobRuns.Add(NewRun("auth-rate-limit-cleanup"));
        if (!await db.BackgroundJobRuns.AnyAsync(x => x.JobName == "session-retention" && x.StartedAt > now.AddHours(-24) && x.Status != "Failed", cancellationToken)) db.BackgroundJobRuns.Add(NewRun("session-retention"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>(); var now = DateTimeOffset.UtcNow;
        await db.BackgroundJobRuns.Where(x => x.Status == "Running" && x.LockedAt < now.AddMinutes(-5)).ExecuteUpdateAsync(x => x.SetProperty(j => j.Status, "Queued").SetProperty(j => j.AvailableAt, now).SetProperty(j => j.LockedAt, (DateTimeOffset?)null), cancellationToken);
        await using var claimTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var claimQuery = db.BackgroundJobRuns.FromSqlRaw("""
            SELECT * FROM "BackgroundJobRuns"
            WHERE "Status" = 'Queued' AND "AvailableAt" <= NOW()
            ORDER BY "AvailableAt", "StartedAt"
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).AsAsyncEnumerable();
        BackgroundJobRun? run = null;
        await foreach (var candidate in claimQuery.WithCancellation(cancellationToken)) { run = candidate; break; }
        if (run is null) { await claimTransaction.CommitAsync(cancellationToken); return false; }
        run.Status = "Running"; run.LockedAt = now; run.Attempts++;
        await db.SaveChangesAsync(cancellationToken);
        await claimTransaction.CommitAsync(cancellationToken);
        try
        {
            if (run.JobName == "history-retention") { var cutoff = now.AddDays(-30); var views = await db.ListingViewEvents.Where(x => x.ViewedAt < cutoff).ExecuteDeleteAsync(cancellationToken); var searches = await db.SearchHistoryEntries.Where(x => x.SearchedAt < cutoff).ExecuteDeleteAsync(cancellationToken); run.Detail = $"Удалено просмотров: {views}, поисков: {searches}."; }
            else if (run.JobName == "saved-search-matches") { var catalog = scope.ServiceProvider.GetRequiredService<IListingCatalog>(); var notifications = 0; foreach (var saved in await db.SavedSearches.ToListAsync(cancellationToken)) { var result = await catalog.SearchAsync(RecommendationQuery.ToSearchRequest(saved.QueryString), cancellationToken); var added = Math.Max(0, result.Total - saved.LastKnownCount); if (added > 0 && saved.NotificationsEnabled) { db.UserNotifications.Add(new UserNotification { Id = Guid.NewGuid(), UserId = saved.UserId, Type = "SavedSearchNewResults", Text = $"Новых объявлений: {added} — {saved.Name}", Link = "/" + saved.QueryString }); notifications++; } saved.LastKnownCount = result.Total; saved.LastCheckedAt = now; } run.Detail = $"Создано уведомлений: {notifications}."; }
            else if (run.JobName == "upload-temp-cleanup") { var removed = UploadTemporaryFileCleanup.RemoveOlderThan(UploadRoots(), now.AddHours(-1)); run.Detail = $"Удалено незавершённых временных файлов: {removed}."; }
            else if (run.JobName == "otp-challenge-cleanup") { var removed = await db.OneTimeCodeChallenges.Where(x => x.ExpiresAt < now).ExecuteDeleteAsync(cancellationToken); run.Detail = $"Удалено просроченных OTP challenges: {removed}."; }
            else if (run.JobName == "auth-rate-limit-cleanup") { var removed = await db.AuthRateLimitBuckets.Where(x => x.ExpiresAt < now).ExecuteDeleteAsync(cancellationToken); run.Detail = $"Удалено просроченных auth rate-limit buckets: {removed}."; }
            else if (run.JobName == "session-retention") { var removed = await db.UserSessions.Where(x => x.CreatedAt < now.AddDays(-30) || (x.RevokedAt != null && x.RevokedAt < now.AddDays(-30))).ExecuteDeleteAsync(cancellationToken); run.Detail = $"Удалено истёкших или старых отозванных сессий: {removed}."; }
            else throw new InvalidOperationException($"Unknown maintenance job: {run.JobName}");
            run.Status = "Completed"; run.CompletedAt = DateTimeOffset.UtcNow; run.LockedAt = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Detail = "Выполнение прервано штатной остановкой приложения; задача возвращена в очередь.";
            run.Status = "Queued";
            run.AvailableAt = DateTimeOffset.UtcNow;
            run.LockedAt = null;
            using var shutdownSave = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try { await db.SaveChangesAsync(shutdownSave.Token); }
            catch (Exception saveException) { logger.LogError(saveException, "Maintenance job {JobId} could not be requeued during shutdown; stale-lock recovery will retry it.", run.Id); }
            metrics.BackgroundJobCompleted(run.JobName, "Queued");
            logger.LogInformation("Maintenance job {JobId} was requeued during graceful shutdown.", run.Id);
            return false;
        }
        catch (Exception ex) { run.Detail = ex.Message[..Math.Min(ex.Message.Length, 900)]; run.LockedAt = null; if (run.Attempts >= 3) { run.Status = "Failed"; run.CompletedAt = DateTimeOffset.UtcNow; } else { run.Status = "Queued"; run.AvailableAt = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, run.Attempts)); } logger.LogWarning(ex, "Maintenance job {JobId} attempt {Attempt} failed.", run.Id, run.Attempts); }
        await db.SaveChangesAsync(cancellationToken);
        metrics.BackgroundJobCompleted(run.JobName, run.Status);
        return true;
    }

    public static BackgroundJobRun NewRun(string jobName, Guid? startedById = null) => new() { Id = Guid.NewGuid(), JobName = jobName, StartedById = startedById, Status = "Queued", AvailableAt = DateTimeOffset.UtcNow };

    private string[] UploadRoots() => [Path.Combine(environment.ContentRootPath, "App_Data", "chat"), Path.Combine(environment.WebRootPath, "uploads")];
}
