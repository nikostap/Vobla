using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Sellers;

public sealed class DetailsModel(MarketplaceDbContext db) : PageModel
{
    public sealed record SellerView(Guid Id, string Name, string City, string? Bio, string? AvatarUrl, bool Verified, DateTimeOffset RegisteredAt, int ActiveListings, double? Rating, int ReviewCount);
    public sealed record ListingView(Guid Id, string Title, string Price, string? Image, string Category);
    public sealed record ReviewView(int Rating, string Text, DateTimeOffset CreatedAt);

    public SellerView? Seller { get; private set; }
    public IReadOnlyList<ListingView> Listings { get; private set; } = [];
    public IReadOnlyList<ReviewView> Reviews { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound();

        var activeListings = db.Listings.AsNoTracking().Where(x => x.OwnerId == id && x.Status == "Active");
        var reviewQuery = db.Reviews.AsNoTracking().Where(x => x.SubjectUserId == id);
        var reviewCount = await reviewQuery.CountAsync(cancellationToken);
        var rating = reviewCount == 0 ? (double?)null : await reviewQuery.AverageAsync(x => (double)x.Rating, cancellationToken);
        var listingCount = await activeListings.CountAsync(cancellationToken);

        Seller = new SellerView(user.Id, user.DisplayName, user.City, user.Bio, user.AvatarUrl, user.EmailConfirmed, user.RegisteredAt, listingCount, rating, reviewCount);
        Listings = await activeListings.OrderByDescending(x => x.PublishedAt).Take(12)
            .Select(x => new ListingView(x.Id, x.Title, x.DealType == "Free" ? "Бесплатно" : x.Price == null ? "Цена не указана" : $"{x.Price:N0} ₽", x.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(), x.Category.Name))
            .ToListAsync(cancellationToken);
        Reviews = await reviewQuery.OrderByDescending(x => x.CreatedAt).Take(8)
            .Select(x => new ReviewView(x.Rating, x.Text, x.CreatedAt)).ToListAsync(cancellationToken);
        return Page();
    }
}
