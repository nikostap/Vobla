using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "Administrator,SeniorAdministrator,Owner,Auditor")]
public sealed class IndexModel(MarketplaceDbContext db) : PageModel
{
    public int Users { get; private set; }
    public int ActiveListings { get; private set; }
    public int PendingModeration { get; private set; }
    public int Conversations { get; private set; }
    public int Deals { get; private set; }
    public int SavedSearches { get; private set; }
    public int AuditEventsToday { get; private set; }
    public IReadOnlyDictionary<string, int> ListingsByStatus { get; private set; } = new Dictionary<string, int>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Users = await db.Users.CountAsync(cancellationToken);
        ActiveListings = await db.Listings.CountAsync(x => x.Status == "Active", cancellationToken);
        PendingModeration = await db.ModerationCases.CountAsync(x => x.Status == "Pending" || x.Status == "Appealed", cancellationToken);
        Conversations = await db.Conversations.CountAsync(cancellationToken);
        Deals = await db.Deals.CountAsync(cancellationToken);
        SavedSearches = await db.SavedSearches.CountAsync(cancellationToken);
        AuditEventsToday = await db.AuditEvents.CountAsync(x => x.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-1), cancellationToken);
        var grouped = await db.Listings.AsNoTracking().GroupBy(x => x.Status).Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(cancellationToken);
        ListingsByStatus = grouped.OrderByDescending(x => x.Count).ToDictionary(x => x.Status, x => x.Count);
    }
}
