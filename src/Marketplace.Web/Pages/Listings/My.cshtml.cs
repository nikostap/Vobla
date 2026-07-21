using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Listings;
[Authorize]
public sealed class MyModel(UserManager<ApplicationUser> users, MarketplaceDbContext db) : PageModel
{
    public sealed record ListingView(Guid Id, string Title, string CategoryName, string Status, string StatusLabel, string PriceLabel, string? ImageUrl, DateTimeOffset UpdatedAt);
    public sealed record RecentView(Guid Id, string Title, string PriceLabel, string? ImageUrl, string? City, DateTimeOffset ViewedAt);
    public IReadOnlyList<ListingView> Listings { get; private set; } = [];
    public IReadOnlyList<RecentView> RecentlyViewed { get; private set; } = [];
    public bool PersonalizationEnabled { get; private set; }
    [TempData] public string? StatusMessage { get; set; }
    public async Task OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return;
        Listings = await db.Listings.AsNoTracking().Where(x => x.OwnerId == user.Id && x.Status != "Deleted").Include(x => x.Category).Include(x => x.Media).OrderByDescending(x => x.UpdatedAt).Select(x => new ListingView(x.Id, x.Title, x.Category.Name, x.Status, x.Status == "Draft" ? "Черновик" : x.Status == "PendingManualReview" ? "На проверке" : x.Status == "Quarantine" ? "Карантин" : x.Status == "Appealed" ? "На апелляции" : x.Status, x.DealType == "Free" ? "Бесплатно" : x.Price == null ? "Цена не указана" : x.Price.Value.ToString("N0") + " ₽", x.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(), x.UpdatedAt)).ToListAsync();
        PersonalizationEnabled = user.UseHistoryForRecommendations;
        if (!PersonalizationEnabled) return;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
        RecentlyViewed = await db.ListingViewEvents.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.ViewedAt >= cutoff && x.Listing.Status == "Active")
            .OrderByDescending(x => x.ViewedAt).Take(100)
            .Select(x => new RecentView(x.ListingId, x.Listing.Title, x.Listing.DealType == "Free" ? "Бесплатно" : x.Listing.Price == null ? "Цена не указана" : $"{x.Listing.Price:N0} ₽", x.Listing.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(), x.Listing.Location == null ? null : x.Listing.Location.City, x.ViewedAt))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm) return RedirectToPage();
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var listing = await db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user.Id && x.Status != "Deleted", cancellationToken);
        if (listing is null) return NotFound();

        var previousStatus = listing.Status;
        listing.Status = "Deleted";
        listing.UpdatedAt = DateTimeOffset.UtcNow;
        db.ListingStatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Status = "Deleted", Reason = "Объявление удалено владельцем." });
        await db.Favorites.Where(x => x.ListingId == listing.Id).ExecuteDeleteAsync(cancellationToken);
        await db.ComparisonItems.Where(x => x.ListingId == listing.Id).ExecuteDeleteAsync(cancellationToken);
        db.AuditEvents.Add(new AuditEvent
        {
            UserId = user.Id,
            EventType = "listing.deleted-by-owner",
            Detail = listing.Title,
            EntityType = "Listing",
            EntityId = listing.Id.ToString(),
            OldValue = previousStatus,
            NewValue = "Deleted",
            Reason = "Удалено владельцем из личного кабинета.",
            CorrelationId = HttpContext.TraceIdentifier,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        StatusMessage = $"Объявление «{listing.Title}» удалено.";
        return RedirectToPage();
    }
}
