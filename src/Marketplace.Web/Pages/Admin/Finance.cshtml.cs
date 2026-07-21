using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Monetization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "FinanceOperator,FinanceController,Administrator,SeniorAdministrator,Owner")]
public sealed class FinanceModel(MarketplaceDbContext db) : PageModel
{
    public sealed record RefundView(Guid Id, Guid PaymentId, string Product, decimal Cash, decimal Bonus, string Reason, string Status, DateTimeOffset RequestedAt);
    public IReadOnlyList<RefundView> Refunds { get; private set; } = [];
    public async Task OnGetAsync() => Refunds = await db.RefundRequests.AsNoTracking().OrderByDescending(r => r.RequestedAt).Take(100).Join(db.PaymentTransactions, r => r.PaymentId, p => p.Id, (r, p) => new RefundView(r.Id, p.Id, p.ProductCode, p.CashAmount, p.BonusUsed, r.Reason, r.Status, r.RequestedAt)).ToListAsync();
    public async Task<IActionResult> OnPostPrepareAsync(Guid id, string note) { var item = await db.RefundRequests.FirstOrDefaultAsync(x => x.Id == id && x.Status == "Pending"); if (item is null || string.IsNullOrWhiteSpace(note)) return RedirectToPage(); item.Status = "Prepared"; item.ResolutionNote = note.Trim(); Audit(item, "Pending", "Prepared", note); await db.SaveChangesAsync(); return RedirectToPage(); }
    public async Task<IActionResult> OnPostResolveAsync(Guid id, bool approve, string note)
    {
        if (!(User.IsInRole("FinanceController") || User.IsInRole("Administrator") || User.IsInRole("SeniorAdministrator") || User.IsInRole("Owner"))) return Forbid(); var item = await db.RefundRequests.FirstOrDefaultAsync(x => x.Id == id && x.Status == "Prepared"); if (item is null || string.IsNullOrWhiteSpace(note)) return RedirectToPage(); var payment = await db.PaymentTransactions.FirstAsync(x => x.Id == item.PaymentId); var before = item.Status; item.Status = approve ? "Approved" : "Rejected"; item.ResolvedById = UserId(); item.ResolvedAt = DateTimeOffset.UtcNow; item.ResolutionNote = note.Trim();
        if (approve) { payment.Status = "Refunded"; db.PaymentTransactions.Add(new PaymentTransaction { Id = Guid.NewGuid(), UserId = payment.UserId, IdempotencyKey = $"refund:{payment.Id}", Kind = "Refund", ProductType = payment.ProductType, ProductCode = payment.ProductCode, ListingId = payment.ListingId, GrossAmount = -payment.GrossAmount, DiscountAmount = -payment.DiscountAmount, BonusUsed = -payment.BonusUsed, CashAmount = -payment.CashAmount, Status = "Succeeded", ParentPaymentId = payment.Id }); if (payment.BonusUsed > 0) db.BonusLedgerEntries.Add(new BonusLedgerEntry { Id = Guid.NewGuid(), UserId = payment.UserId, Amount = payment.BonusUsed, Reason = $"Возврат {payment.ProductCode}", PaymentId = payment.Id }); foreach (var promo in await db.PromotionPurchases.Where(x => x.PaymentId == payment.Id).ToListAsync()) promo.Status = "Refunded"; foreach (var entitlement in await db.Entitlements.Where(x => x.UserId == payment.UserId && x.PlanCode == payment.ProductCode && (x.EndsAt == null || x.EndsAt > DateTimeOffset.UtcNow)).ToListAsync()) entitlement.EndsAt = DateTimeOffset.UtcNow; }
        Audit(item, before, item.Status, note); await db.SaveChangesAsync(); return RedirectToPage();
    }
    private void Audit(RefundRequest item, string oldValue, string newValue, string reason) => db.AuditEvents.Add(new AuditEvent { UserId = UserId(), EventType = "finance.refund", Detail = $"Refund {item.Id}", ActorRole = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value, EntityType = "RefundRequest", EntityId = item.Id.ToString(), OldValue = oldValue, NewValue = newValue, Reason = reason.Trim(), CorrelationId = HttpContext.TraceIdentifier, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
    private Guid UserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
