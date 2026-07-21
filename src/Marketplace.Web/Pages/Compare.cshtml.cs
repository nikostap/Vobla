using System.Security.Claims;
using System.Text.Json;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages;

[Authorize]
public sealed class CompareModel(MarketplaceDbContext db) : PageModel
{
    public sealed record Item(Guid Id, string Title, string Price, Guid SellerId, string Seller, string Condition, string? Image, Dictionary<string, string> Attributes);

    public IReadOnlyList<Item> Items { get; private set; } = [];
    public IReadOnlyList<string> AttributeNames { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var entities = await db.ComparisonItems.AsNoTracking()
            .Where(x => x.UserId == userId && x.Listing.Status != "Deleted")
            .Include(x => x.Listing).ThenInclude(x => x.Media)
            .Include(x => x.Listing).ThenInclude(x => x.Owner)
            .Include(x => x.Listing).ThenInclude(x => x.AttributeValues)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Listing)
            .ToListAsync(cancellationToken);
        var codes = entities.SelectMany(x => x.AttributeValues.Select(a => a.AttributeCode)).Distinct().ToList();
        var names = await db.CategoryAttributes.AsNoTracking().Where(x => codes.Contains(x.Code)).GroupBy(x => x.Code).ToDictionaryAsync(x => x.Key, x => x.First().Name, cancellationToken);
        Items = entities.Select(x => new Item(
            x.Id,
            x.Title,
            x.DealType == "Free" ? "Бесплатно" : x.Price == null ? "—" : $"{x.Price:N0} ₽",
            x.OwnerId,
            x.Owner.DisplayName,
            x.Condition == "New" ? "Новое" : "Б/у",
            x.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(),
            x.AttributeValues.ToDictionary(a => names.GetValueOrDefault(a.AttributeCode, a.AttributeCode), a => JsonSerializer.Deserialize<string>(a.ValueJson) ?? "—")))
            .ToList();
        AttributeNames = Items.SelectMany(x => x.Attributes.Keys).Distinct().Order().ToList();
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        var stored = await db.ComparisonItems.FirstOrDefaultAsync(x => x.UserId == CurrentUserId() && x.ListingId == id, cancellationToken);
        if (stored is not null)
        {
            db.ComparisonItems.Remove(stored);
            await db.SaveChangesAsync(cancellationToken);
            TempData["Notice"] = "Объявление удалено из сравнения.";
        }
        return RedirectToPage();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
