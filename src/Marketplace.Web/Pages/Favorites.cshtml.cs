using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages;

[Authorize]
public sealed class FavoritesModel(MarketplaceDbContext db) : PageModel
{
    public sealed record Item(Guid Id, string Title, string Category, string Price, decimal? OldPrice, string Status, string? Image, Guid SellerId, string SellerName);

    public IReadOnlyList<Item> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        Items = await db.Favorites.AsNoTracking()
            .Where(x => x.UserId == userId && x.Listing.Status != "Deleted")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new Item(
                x.ListingId,
                x.Listing.Title,
                x.Listing.Category.Name,
                x.Listing.DealType == "Free" ? "Бесплатно" : x.Listing.Price == null ? "—" : x.Listing.Price.Value.ToString("N0") + " ₽",
                x.PriceWhenAdded != x.Listing.Price ? x.PriceWhenAdded : null,
                x.Listing.Status,
                x.Listing.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(),
                x.Listing.OwnerId,
                x.Listing.Owner.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var stored = await db.Favorites.FirstOrDefaultAsync(x => x.UserId == CurrentUserId() && x.ListingId == id, cancellationToken);
        if (stored is not null)
        {
            db.Favorites.Remove(stored);
            await db.SaveChangesAsync(cancellationToken);
            TempData["Notice"] = "Объявление удалено из избранного.";
        }
        return RedirectToPage();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
