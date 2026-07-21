using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Account;

[Authorize]
public sealed class SessionsModel(UserManager<ApplicationUser> userManager, MarketplaceDbContext db) : PageModel
{
    public sealed record SessionView(Guid Id, string Device, string IpAddress, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt, bool IsCurrent);
    public IReadOnlyList<SessionView> Sessions { get; private set; } = [];
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User); if (user is null) return Challenge();
        var currentId = Guid.TryParse(User.FindFirst("session_id")?.Value, out var parsed) ? parsed : Guid.Empty;
        Sessions = await db.UserSessions.AsNoTracking().Where(x => x.UserId == user.Id && x.RevokedAt == null).OrderByDescending(x => x.LastSeenAt).Select(x => new SessionView(x.Id, x.Device, x.IpAddress, x.CreatedAt, x.LastSeenAt, x.Id == currentId)).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid sessionId)
    {
        var user = await userManager.GetUserAsync(User); if (user is null) return Challenge();
        var currentId = Guid.TryParse(User.FindFirst("session_id")?.Value, out var parsed) ? parsed : Guid.Empty;
        var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == user.Id && x.RevokedAt == null);
        if (session is not null && session.Id != currentId)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            db.AuditEvents.Add(new AuditEvent { UserId = user.Id, EventType = "session.revoked", Detail = $"Завершена сессия {session.Device}.", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
            await db.SaveChangesAsync(); StatusMessage = "Сессия завершена.";
        }
        return RedirectToPage();
    }
}
