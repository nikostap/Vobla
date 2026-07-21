using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "Administrator,SeniorAdministrator,Owner,SecuritySpecialist")]
public sealed class PrivacyModel(MarketplaceDbContext db) : PageModel
{
    public sealed record RequestView(Guid Id, string Email, string DisplayName, string Status, DateTimeOffset RequestedAt);
    public IReadOnlyList<RequestView> Requests { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) => Requests = await db.AccountErasureRequests.AsNoTracking()
        .Where(x => x.Status == "Pending").OrderBy(x => x.RequestedAt)
        .Select(x => new RequestView(x.Id, x.User.Email ?? "", x.User.DisplayName, x.Status, x.RequestedAt)).ToListAsync(cancellationToken);

    public async Task<IActionResult> OnPostResolveAsync(Guid id, bool approve, string note, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length < 5) return RedirectToPage();
        var actorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var status = approve ? "Approved" : "Rejected";
        var reason = note.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.AccountErasureRequests.Where(x => x.Id == id && x.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, status).SetProperty(x => x.ResolvedById, actorId).SetProperty(x => x.ResolutionNote, reason).SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        if (updated != 1) return StatusCode(StatusCodes.Status409Conflict);
        db.AuditEvents.Add(new AuditEvent { UserId = actorId, EventType = "admin.privacy.erasure-resolved", Detail = $"Account erasure request {id}", ActorRole = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value, EntityType = "AccountErasureRequest", EntityId = id.ToString(), OldValue = "Pending", NewValue = status, Reason = reason, CorrelationId = HttpContext.TraceIdentifier, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RedirectToPage();
    }
}
