using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Account;

[Authorize]
public sealed class SignOutModel(SignInManager<ApplicationUser> signInManager, MarketplaceDbContext db) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Account/Profile");
    public async Task<IActionResult> OnPostAsync()
    {
        if (Guid.TryParse(User.FindFirst("session_id")?.Value, out var sessionId))
        {
            var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == sessionId);
            if (session is not null)
            {
                session.RevokedAt = DateTimeOffset.UtcNow;
                db.AuditEvents.Add(new AuditEvent { UserId = session.UserId, EventType = "account.signed_out", Detail = "Пользователь завершил текущую сессию.", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
                await db.SaveChangesAsync();
            }
        }
        await signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }
}
