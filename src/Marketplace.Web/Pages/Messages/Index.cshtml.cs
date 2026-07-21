using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Messages;

[Authorize]
public sealed class IndexModel(MarketplaceDbContext db) : PageModel
{
    public sealed record DialogView(Guid Id, string ListingTitle, string OtherName, string LastMessage, DateTimeOffset UpdatedAt, int Unread, string? Image);
    public IReadOnlyList<DialogView> Dialogs { get; private set; } = [];
    public async Task OnGetAsync()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var source = await db.Conversations.AsNoTracking().Where(x => (x.SellerId == userId && !x.IsArchivedBySeller) || (x.BuyerId == userId && !x.IsArchivedByBuyer)).Include(x => x.Listing).ThenInclude(x => x.Media).Include(x => x.Seller).Include(x => x.Buyer).Include(x => x.Messages).OrderByDescending(x => x.UpdatedAt).ToListAsync();
        Dialogs = source.Select(x => new DialogView(x.Id, x.Listing.Title, x.SellerId == userId ? x.Buyer.DisplayName : x.Seller.DisplayName, x.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.Text).FirstOrDefault() ?? "Диалог без сообщений", x.UpdatedAt, x.Messages.Count(m => m.SenderId != userId && m.ReadAt == null), x.Listing.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault())).ToList();
    }
}
