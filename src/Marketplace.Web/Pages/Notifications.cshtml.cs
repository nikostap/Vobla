using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages;

[Authorize]
public sealed class NotificationsModel(MarketplaceDbContext db) : PageModel
{
    public sealed record NotificationView(Guid Id, string Text, string TypeLabel, string? Link, DateTimeOffset CreatedAt, bool IsRead);

    public IReadOnlyList<NotificationView> Items { get; private set; } = [];
    public int UnreadCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        Items = await db.UserNotifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationView(
                x.Id,
                x.Text,
                x.Type == "PriceDrop" ? "Цена" : x.Type == "SavedSearchNewResults" ? "Поиск" : x.Type == "DealRequested" ? "Сделка" : x.Type == "DealConfirmed" ? "Сделка" : "Событие",
                x.Link,
                x.CreatedAt,
                x.ReadAt != null))
            .ToListAsync(cancellationToken);
        UnreadCount = Items.Count(x => !x.IsRead);
    }

    public async Task<IActionResult> OnPostOpenAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId(), cancellationToken);
        if (item is null) return NotFound();
        item.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(item.Link) && Url.IsLocalUrl(item.Link) ? LocalRedirect(item.Link) : RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId(), cancellationToken);
        if (item is not null)
        {
            item.ReadAt ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        await db.UserNotifications.Where(x => x.UserId == userId && x.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReadAt, DateTimeOffset.UtcNow), cancellationToken);
        TempData["Notice"] = "Все уведомления отмечены прочитанными.";
        return RedirectToPage();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
