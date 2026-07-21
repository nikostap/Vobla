using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "Administrator,SeniorAdministrator,Owner")]
public sealed class UsersModel(MarketplaceDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole<Guid>> roleManager) : PageModel
{
    public sealed record UserView(Guid Id, string Email, string DisplayName, string City, DateTimeOffset RegisteredAt, IReadOnlyList<string> Roles, bool IsModerator);
    public IReadOnlyList<UserView> Users { get; private set; } = [];
    public IReadOnlyList<string> Roles { get; private set; } = [];
    [TempData] public string? StatusMessage { get; set; }

    public async Task OnGetAsync(string? q)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => (x.Email != null && x.Email.Contains(q)) || x.DisplayName.Contains(q));
        var users = await query.OrderByDescending(x => x.RegisteredAt).Take(100).ToListAsync();
        var views = new List<UserView>();
        foreach (var user in users)
        {
            var assigned = (await userManager.GetRolesAsync(user)).ToArray();
            views.Add(new UserView(user.Id, user.Email ?? "", user.DisplayName, user.City, user.RegisteredAt, assigned, assigned.Contains("Moderator")));
        }
        Users = views;
        Roles = await roleManager.Roles.OrderBy(x => x.Name).Select(x => x.Name!).ToListAsync();
    }

    public async Task<IActionResult> OnPostRoleAsync(Guid id, string role, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || !await roleManager.RoleExistsAsync(role)) return RedirectToPage();
        var target = await userManager.FindByIdAsync(id.ToString()); if (target is null) return NotFound();
        var before = await userManager.GetRolesAsync(target);
        if (!before.Contains(role)) await userManager.AddToRoleAsync(target, role);
        db.AuditEvents.Add(AdminAudit("admin.user-role.added", target.Id, string.Join(",", before), string.Join(",", before.Append(role).Distinct()), reason));
        await db.SaveChangesAsync(); return RedirectToPage();
    }

    public async Task<IActionResult> OnPostModeratorAsync(Guid id, bool enabled, string reason)
    {
        var normalizedReason = reason?.Trim() ?? string.Empty;
        if (normalizedReason.Length < 5) return RedirectToPage();
        var target = await userManager.FindByIdAsync(id.ToString());
        if (target is null) return NotFound();
        var before = await userManager.GetRolesAsync(target);
        var hasRole = before.Contains("Moderator");
        if (hasRole == enabled)
        {
            StatusMessage = enabled ? "Пользователь уже является модератором." : "У пользователя уже нет роли модератора.";
            return RedirectToPage();
        }

        var result = enabled
            ? await userManager.AddToRoleAsync(target, "Moderator")
            : await userManager.RemoveFromRoleAsync(target, "Moderator");
        if (!result.Succeeded) return StatusCode(StatusCodes.Status409Conflict);

        var after = enabled ? before.Append("Moderator").Distinct().ToArray() : before.Where(x => x != "Moderator").ToArray();
        db.AuditEvents.Add(AdminAudit(enabled ? "admin.user-moderator.assigned" : "admin.user-moderator.removed", target.Id, string.Join(",", before), string.Join(",", after), normalizedReason));
        await db.SaveChangesAsync();
        StatusMessage = enabled ? $"{target.DisplayName} назначен модератором." : $"Роль модератора снята с {target.DisplayName}.";
        return RedirectToPage();
    }

    private AuditEvent AdminAudit(string eventType, Guid targetId, string oldValue, string newValue, string reason) => new() { UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), EventType = eventType, Detail = $"Role update for user {targetId}", ActorRole = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value, EntityType = "User", EntityId = targetId.ToString(), OldValue = oldValue, NewValue = newValue, Reason = reason.Trim(), CorrelationId = HttpContext.TraceIdentifier, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" };
}
