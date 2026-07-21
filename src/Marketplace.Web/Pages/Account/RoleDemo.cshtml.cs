using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Account;

[Authorize]
public sealed class RoleDemoModel(UserManager<ApplicationUser> userManager, MarketplaceDbContext db) : PageModel
{
    public sealed record RoleView(string Title, string Description, bool IsCurrent);
    public sealed record AuditView(string EventType, string Detail, DateTimeOffset CreatedAt);
    public IReadOnlyList<RoleView> Roles { get; private set; } = [];
    public IReadOnlyList<AuditView> AuditEvents { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User); if (user is null) return Challenge();
        var assigned = await userManager.GetRolesAsync(user);
        Roles =
        [
            new("Пользователь", "Профиль, объявления, избранное, сообщения и отзывы.", assigned.Contains("Member")),
            new("Модератор", "Очередь проверок, findings и решения по публикациям.", assigned.Contains("Moderator")),
            new("Администратор", "Управление ролями, каталогом и операционными настройками.", assigned.Contains("Administrator"))
        ];
        AuditEvents = await db.AuditEvents.AsNoTracking().Where(x => x.UserId == user.Id).OrderByDescending(x => x.CreatedAt).Take(20).Select(x => new AuditView(x.EventType, x.Detail, x.CreatedAt)).ToListAsync();
        return Page();
    }
}
