using System.Security.Claims;
using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Engagement;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages;

[Authorize]
public sealed class RecommendationsModel(MarketplaceDbContext db, IListingCatalog listingCatalog) : PageModel
{
    public sealed record SavedSearchView(Guid Id, string Name, string Url, int Count, int NewCount, bool NotificationsEnabled, DateTimeOffset LastCheckedAt);
    public sealed record ListingView(Guid Id, string Title, string PriceLabel, string? Image, string? City, DateTimeOffset ViewedAt);
    public sealed record SearchView(string Label, string Url, DateTimeOffset SearchedAt);

    public IReadOnlyList<SavedSearchView> SavedSearches { get; private set; } = [];
    public IReadOnlyList<ListingView> RecentlyViewed { get; private set; } = [];
    public IReadOnlyList<SearchView> SearchHistory { get; private set; } = [];
    public bool PersonalizationEnabled { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = UserId();
        var user = await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId, cancellationToken);
        PersonalizationEnabled = user.UseHistoryForRecommendations;
        var saved = await db.SavedSearches.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        var views = new List<SavedSearchView>();
        foreach (var item in saved)
        {
            var result = await listingCatalog.SearchAsync(Modules.Recommendations.RecommendationQuery.ToSearchRequest(item.QueryString), cancellationToken);
            var newCount = Math.Max(0, result.Total - item.LastKnownCount);
            if (newCount > 0 && item.NotificationsEnabled)
            {
                db.UserNotifications.Add(new UserNotification { Id = Guid.NewGuid(), UserId = userId, Type = "SavedSearchNewResults", Text = $"Новых объявлений: {newCount} — {item.Name}", Link = "/" + item.QueryString });
            }
            item.LastKnownCount = result.Total;
            item.LastCheckedAt = DateTimeOffset.UtcNow;
            views.Add(new SavedSearchView(item.Id, item.Name, "/" + item.QueryString, result.Total, newCount, item.NotificationsEnabled, item.LastCheckedAt));
        }
        if (saved.Count > 0) await db.SaveChangesAsync(cancellationToken);
        SavedSearches = views;

        if (PersonalizationEnabled)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            await db.ListingViewEvents.Where(x => x.UserId == userId && x.ViewedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
            await db.SearchHistoryEntries.Where(x => x.UserId == userId && x.SearchedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
            RecentlyViewed = await db.ListingViewEvents.AsNoTracking().Where(x => x.UserId == userId && x.ViewedAt >= cutoff && x.Listing.Status == "Active").OrderByDescending(x => x.ViewedAt).Take(12)
                .Select(x => new ListingView(x.ListingId, x.Listing.Title, x.Listing.DealType == "Free" ? "Бесплатно" : x.Listing.Price == null ? "Цена не указана" : $"{x.Listing.Price:N0} ₽", x.Listing.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(), x.Listing.Location == null ? null : x.Listing.Location.City, x.ViewedAt)).ToListAsync(cancellationToken);
            SearchHistory = await db.SearchHistoryEntries.AsNoTracking().Where(x => x.UserId == userId && x.SearchedAt >= cutoff).OrderByDescending(x => x.SearchedAt).Take(12).Select(x => new SearchView(x.Label, "/" + x.QueryString, x.SearchedAt)).ToListAsync(cancellationToken);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostClearHistoryAsync(CancellationToken cancellationToken)
    {
        var userId = UserId();
        await db.ListingViewEvents.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.SearchHistoryEntries.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        db.AuditEvents.Add(new AuditEvent { UserId = userId, EventType = "recommendations.history-cleared", Detail = "История просмотров и поиска удалена.", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync(cancellationToken);
        TempData["Notice"] = "История просмотров и поиска очищена.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SavedSearches.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId(), cancellationToken);
        if (item is not null) { db.SavedSearches.Remove(item); await db.SaveChangesAsync(cancellationToken); }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleNotificationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.SavedSearches.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId(), cancellationToken);
        if (item is not null) { item.NotificationsEnabled = !item.NotificationsEnabled; await db.SaveChangesAsync(cancellationToken); }
        return RedirectToPage();
    }

    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
