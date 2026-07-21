using System.Security.Claims;
using Marketplace.Web.Modules.Administration;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "Administrator,SeniorAdministrator,Owner,Auditor")]
public sealed class OperationsModel(MarketplaceDbContext db) : PageModel
{
    public IReadOnlyList<FeatureFlag> Flags { get; private set; } = [];
    public IReadOnlyList<BackgroundJobRun> Jobs { get; private set; } = [];
    public IReadOnlyList<AuditEvent> Audit { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Flags = await db.FeatureFlags.AsNoTracking().OrderBy(x => x.Key).ToListAsync();
        Jobs = await db.BackgroundJobRuns.AsNoTracking().OrderByDescending(x => x.StartedAt).Take(30).ToListAsync();
        Audit = await db.AuditEvents.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync();
    }

    public async Task<IActionResult> OnPostFlagAsync(Guid id, string reason)
    {
        if (User.IsInRole("Auditor") || string.IsNullOrWhiteSpace(reason)) return Forbid();
        var item = await db.FeatureFlags.FirstOrDefaultAsync(x => x.Id == id); if (item is null) return NotFound(); var before = item.IsEnabled; item.IsEnabled = !item.IsEnabled; item.UpdatedAt = DateTimeOffset.UtcNow; item.UpdatedById = UserId(); db.AuditEvents.Add(NewAudit("admin.feature-flag", "FeatureFlag", item.Id.ToString(), before.ToString(), item.IsEnabled.ToString(), reason)); await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRunJobAsync(string job)
    {
        if (User.IsInRole("Auditor")) return Forbid();
        if (job is not ("history-retention" or "saved-search-matches" or "upload-temp-cleanup" or "otp-challenge-cleanup" or "auth-rate-limit-cleanup" or "session-retention")) return NotFound();
        var run = MaintenanceJobWorker.NewRun(job, UserId()); db.BackgroundJobRuns.Add(run); db.AuditEvents.Add(NewAudit("admin.job.queued", "BackgroundJob", run.Id.ToString(), null, "Queued", job)); await db.SaveChangesAsync(); return RedirectToPage();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private AuditEvent NewAudit(string type, string entity, string id, string? oldValue, string? newValue, string reason) => new() { UserId = UserId(), EventType = type, Detail = $"{entity} {id}", ActorRole = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value, EntityType = entity, EntityId = id, OldValue = oldValue, NewValue = newValue, Reason = reason.Trim(), CorrelationId = HttpContext.TraceIdentifier, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" };
}
