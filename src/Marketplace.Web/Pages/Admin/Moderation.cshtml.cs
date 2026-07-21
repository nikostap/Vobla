using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;
[Authorize(Roles = "Moderator,Administrator")]
public sealed class ModerationModel(MarketplaceDbContext db) : PageModel
{
    public sealed record CaseView(Guid Id, string Title, string Category, string PriceLabel, string RiskLevel, string RuleSetVersion, int FindingsCount, DateTimeOffset CreatedAt, bool IsAppeal);
    public IReadOnlyList<CaseView> Cases { get; private set; } = [];
    public int PendingCount { get; private set; } public int CriticalCount { get; private set; } public int ResolvedToday { get; private set; }
    public async Task OnGetAsync() { var query = db.ModerationCases.AsNoTracking(); PendingCount = await query.CountAsync(x => x.Status == "Pending" || x.Status == "Appealed"); CriticalCount = await query.CountAsync(x => (x.Status == "Pending" || x.Status == "Appealed") && x.RiskLevel == "Critical"); var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero); ResolvedToday = await query.CountAsync(x => x.ResolvedAt >= today); Cases = await query.Where(x => x.Status == "Pending" || x.Status == "Appealed").OrderByDescending(x => x.Status == "Appealed").ThenByDescending(x => x.RiskLevel == "Critical").ThenByDescending(x => x.RiskLevel == "High").ThenBy(x => x.CreatedAt).Select(x => new CaseView(x.Id, x.Listing.Title, x.Listing.Category.Name, x.Listing.DealType == "Free" ? "Бесплатно" : x.Listing.Price == null ? "Цена не указана" : x.Listing.Price.Value.ToString("N0") + " ₽", x.RiskLevel, x.RuleSetVersion, x.Findings.Count, x.CreatedAt, x.Status == "Appealed")).ToListAsync(); }
}
