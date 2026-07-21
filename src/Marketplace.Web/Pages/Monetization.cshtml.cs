using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Monetization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages;

[Authorize]
public sealed class MonetizationModel(MarketplaceDbContext db, SandboxPaymentService payments, QuotaService quotas) : PageModel
{
    public sealed record ListingOption(Guid Id, string Title);
    public IReadOnlyList<PlanDefinition> Plans { get; private set; } = [];
    public IReadOnlyList<PromotionProduct> Products { get; private set; } = [];
    public IReadOnlyList<ListingOption> Listings { get; private set; } = [];
    public IReadOnlyList<PaymentTransaction> Transactions { get; private set; } = [];
    public IReadOnlySet<Guid> RefundedOrRequested { get; private set; } = new HashSet<Guid>();
    public decimal BonusBalance { get; private set; }
    public int ActiveListingLimit { get; private set; }
    public string IdempotencyKey { get; private set; } = Guid.NewGuid().ToString("N");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = UserId(); Plans = await db.PlanDefinitions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.MonthlyPrice).ToListAsync(cancellationToken); Products = await db.PromotionProducts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Price).ToListAsync(cancellationToken); Listings = await db.Listings.AsNoTracking().Where(x => x.OwnerId == userId && x.Status != "Deleted").OrderByDescending(x => x.UpdatedAt).Select(x => new ListingOption(x.Id, x.Title)).ToListAsync(cancellationToken); Transactions = await db.PaymentTransactions.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).Take(30).ToListAsync(cancellationToken); RefundedOrRequested = (await db.RefundRequests.AsNoTracking().Where(x => x.RequestedById == userId).Select(x => x.PaymentId).ToListAsync(cancellationToken)).ToHashSet(); BonusBalance = await payments.BonusBalanceAsync(userId, cancellationToken); ActiveListingLimit = await quotas.ActiveListingLimitAsync(userId, cancellationToken);
    }

    public async Task<IActionResult> OnPostPurchaseAsync(string productType, string productCode, Guid? listingId, string? promoCode, bool useBonus, string idempotencyKey, CancellationToken cancellationToken)
    {
        try { var result = await payments.PurchaseAsync(UserId(), productType, productCode, listingId, promoCode, useBonus, idempotencyKey, cancellationToken); TempData["Notice"] = result.WasExisting ? "Повторный запрос безопасно вернул существующий платёж." : $"Sandbox-оплата выполнена: {result.CashAmount:N0} ₽."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRefundAsync(Guid paymentId, string reason, CancellationToken cancellationToken)
    {
        var userId = UserId(); var payment = await db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == paymentId && x.UserId == userId && x.Kind == "Purchase" && x.Status == "Succeeded", cancellationToken); if (payment is null || string.IsNullOrWhiteSpace(reason) || await db.RefundRequests.AnyAsync(x => x.PaymentId == paymentId, cancellationToken)) return RedirectToPage(); payment.Status = "RefundRequested"; db.RefundRequests.Add(new RefundRequest { Id = Guid.NewGuid(), PaymentId = paymentId, RequestedById = userId, Reason = reason.Trim() }); await db.SaveChangesAsync(cancellationToken); TempData["Notice"] = "Запрос на возврат передан финансовому оператору."; return RedirectToPage();
    }
    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
