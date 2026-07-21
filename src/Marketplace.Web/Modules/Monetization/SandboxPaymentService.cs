using Marketplace.Web.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Monetization;

public sealed record SandboxPurchaseResult(Guid PaymentId, bool WasExisting, string Status, decimal CashAmount);

public sealed class SandboxPaymentService(MarketplaceDbContext db)
{
    public async Task<decimal> BonusBalanceAsync(Guid userId, CancellationToken cancellationToken = default) => await db.BonusLedgerEntries.Where(x => x.UserId == userId && (x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow)).SumAsync(x => x.Amount, cancellationToken);

    public async Task<SandboxPurchaseResult> PurchaseAsync(Guid userId, string productType, string productCode, Guid? listingId, string? promoCode, bool useBonus, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var existing = await db.PaymentTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return new(existing.Id, true, existing.Status, existing.CashAmount);
        decimal gross; int durationDays; int listingLimit = 0;
        if (productType == "Plan") { var plan = await db.PlanDefinitions.AsNoTracking().Where(x => x.Code == productCode && x.IsActive).OrderByDescending(x => x.Version).FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Тариф не найден."); gross = plan.MonthlyPrice; durationDays = 30; listingLimit = plan.ActiveListingLimit; }
        else { var product = await db.PromotionProducts.AsNoTracking().Where(x => x.Code == productCode && x.IsActive).OrderByDescending(x => x.PriceVersion).FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Продукт не найден."); if (listingId is null || !await db.Listings.AnyAsync(x => x.Id == listingId && x.OwnerId == userId, cancellationToken)) throw new InvalidOperationException("Объявление недоступно."); gross = product.Price; durationDays = product.DurationDays; }
        var promo = string.IsNullOrWhiteSpace(promoCode) ? null : await db.PromoCodes.FirstOrDefaultAsync(x => x.Code == promoCode.Trim().ToUpper() && x.IsActive && x.UsedCount < x.MaxUses && (x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow), cancellationToken);
        var discount = promo is null ? 0 : decimal.Round(gross * promo.DiscountPercent / 100m, 2);
        var payable = gross - discount; var balance = useBonus ? await BonusBalanceAsync(userId, cancellationToken) : 0; var bonusUsed = Math.Min(Math.Max(0, balance), payable); var cash = payable - bonusUsed;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var payment = new PaymentTransaction { Id = Guid.NewGuid(), UserId = userId, IdempotencyKey = idempotencyKey, ProductType = productType, ProductCode = productCode, ListingId = listingId, GrossAmount = gross, DiscountAmount = discount, BonusUsed = bonusUsed, CashAmount = cash, Status = "Succeeded" };
        db.PaymentTransactions.Add(payment);
        if (bonusUsed > 0) db.BonusLedgerEntries.Add(new BonusLedgerEntry { Id = Guid.NewGuid(), UserId = userId, Amount = -bonusUsed, Reason = $"Оплата {productCode}", PaymentId = payment.Id });
        if (promo is not null) { promo.UsedCount++; if (promo.BonusAmount > 0) db.BonusLedgerEntries.Add(new BonusLedgerEntry { Id = Guid.NewGuid(), UserId = userId, Amount = promo.BonusAmount, Reason = $"Промокод {promo.Code}", PaymentId = payment.Id, ExpiresAt = DateTimeOffset.UtcNow.AddYears(1) }); }
        if (productType == "Plan") db.Entitlements.Add(new Entitlement { Id = Guid.NewGuid(), UserId = userId, PlanCode = productCode, ActiveListingLimit = listingLimit, EndsAt = DateTimeOffset.UtcNow.AddDays(durationDays) });
        else db.PromotionPurchases.Add(new PromotionPurchase { Id = Guid.NewGuid(), UserId = userId, ListingId = listingId!.Value, ProductCode = productCode, PaymentId = payment.Id, EndsAt = DateTimeOffset.UtcNow.AddDays(durationDays) });
        await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return new(payment.Id, false, payment.Status, cash);
    }
}
