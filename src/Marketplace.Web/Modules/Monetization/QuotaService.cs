using Marketplace.Web.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Monetization;

public sealed class QuotaService(MarketplaceDbContext db)
{
    public async Task<int> ActiveListingLimitAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Entitlements.AsNoTracking().Where(x => x.UserId == userId && x.StartsAt <= DateTimeOffset.UtcNow && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)).OrderByDescending(x => x.ActiveListingLimit).Select(x => x.ActiveListingLimit).FirstOrDefaultAsync(cancellationToken) is var limit && limit > 0 ? limit : 10;
}
