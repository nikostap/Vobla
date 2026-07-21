using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Account;

[Authorize]
public sealed class DataModel(MarketplaceDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    public AccountErasureRequest? PendingErasureRequest { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            PendingErasureRequest = await db.AccountErasureRequests.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.Status == "Pending", cancellationToken);
    }

    public async Task<IActionResult> OnPostRequestErasureAsync(bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm || !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return RedirectToPage();
        if (!await db.AccountErasureRequests.AnyAsync(x => x.UserId == userId && x.Status == "Pending", cancellationToken))
        {
            var request = new AccountErasureRequest { Id = Guid.NewGuid(), UserId = userId };
            db.AccountErasureRequests.Add(request);
            db.AuditEvents.Add(UserAudit(userId, "privacy.erasure.requested", request.Id, "Pending"));
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                if (!await db.AccountErasureRequests.AnyAsync(x => x.UserId == userId && x.Status == "Pending", cancellationToken)) throw;
            }
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelErasureAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var updated = await db.AccountErasureRequests.Where(x => x.Id == id && x.UserId == userId && x.Status == "Pending")
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, "Cancelled").SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        if (updated != 1) return StatusCode(StatusCodes.Status409Conflict);
        db.AuditEvents.Add(UserAudit(userId, "privacy.erasure.cancelled", id, "Cancelled"));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();

        var identityUser = await userManager.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("Аккаунт не найден.");
        var roles = await userManager.GetRolesAsync(identityUser);
        var profile = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new
        {
            x.Id, x.Email, x.EmailConfirmed, x.DisplayName, x.City, x.Address, x.CityLatitude, x.CityLongitude, x.Bio, x.RegisteredAt,
            settings = new { x.ShowPhone, x.ShowEmail, x.AllowMessages, x.ShowExactAddress, x.ShowOnlineStatus, x.UseHistoryForRecommendations, x.EmailNotifications }
        }).SingleAsync(cancellationToken);
        var sessions = await db.UserSessions.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.Device, x.IpAddress, x.CreatedAt, x.LastSeenAt, x.RevokedAt }).ToListAsync(cancellationToken);
        var audit = await db.AuditEvents.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.EventType, x.Detail, x.IpAddress, x.ActorRole, x.EntityType, x.EntityId, x.OldValue, x.NewValue, x.Reason, x.CorrelationId, x.CreatedAt }).ToListAsync(cancellationToken);
        var listings = await db.Listings.AsNoTracking().Where(x => x.OwnerId == userId).OrderByDescending(x => x.CreatedAt).Select(x => new
        {
            x.Id, category = x.Category.Name, x.Title, x.Description, x.DealType, x.Price, x.Condition, x.Status, x.AllowMessages, x.ShowPhone, x.Phone,
            x.CreatedAt, x.UpdatedAt, x.PublishedAt, x.RecreatedFromListingId,
            media = x.Media.OrderBy(m => m.SortOrder).Select(m => new { m.Id, m.Url, m.Alt, m.SortOrder, m.IsPrimary }).ToList(),
            attributes = x.AttributeValues.Select(a => new { a.AttributeCode, a.ValueJson }).ToList(),
            location = x.Location == null ? null : new { x.Location.City, x.Location.District, x.Location.ExactAddress, x.Location.ExactLatitude, x.Location.ExactLongitude, x.Location.PublicLatitude, x.Location.PublicLongitude, x.Location.IsExactPointPublic }
        }).ToListAsync(cancellationToken);
        var conversations = await db.Conversations.AsNoTracking().Where(x => x.SellerId == userId || x.BuyerId == userId).OrderByDescending(x => x.UpdatedAt).Select(x => new
        {
            x.Id, x.ListingId, x.SellerId, x.BuyerId, x.IsArchivedBySeller, x.IsArchivedByBuyer, x.CreatedAt, x.UpdatedAt,
            messages = x.Messages.OrderBy(m => m.CreatedAt).Select(m => new
            {
                m.Id, m.SenderId, m.Kind, m.Text, m.ReplyToMessageId, m.CreatedAt, m.DeliveredAt, m.ReadAt,
                attachments = m.Attachments.Select(a => new { a.Id, a.OriginalName, a.MimeType, a.Size, a.SafetyStatus }).ToList()
            }).ToList()
        }).ToListAsync(cancellationToken);
        var engagement = new
        {
            favorites = await db.Favorites.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.ListingId, x.PriceWhenAdded, x.CreatedAt }).ToListAsync(cancellationToken),
            comparisons = await db.ComparisonItems.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.ListingId, x.CreatedAt }).ToListAsync(cancellationToken),
            deals = await db.Deals.AsNoTracking().Where(x => x.SellerId == userId || x.BuyerId == userId).Select(x => new { x.Id, x.ConversationId, x.ListingId, x.SellerId, x.BuyerId, x.Status, x.CreatedAt, x.ConfirmedAt }).ToListAsync(cancellationToken),
            reviews = await db.Reviews.AsNoTracking().Where(x => x.AuthorId == userId || x.SubjectUserId == userId).Select(x => new { x.Id, x.DealId, x.AuthorId, x.SubjectUserId, x.Rating, x.Text, x.CreatedAt }).ToListAsync(cancellationToken),
            notifications = await db.UserNotifications.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.Type, x.Text, x.Link, x.CreatedAt, x.ReadAt }).ToListAsync(cancellationToken)
        };
        var recommendations = new
        {
            listingViews = await db.ListingViewEvents.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.ListingId, x.ViewedAt }).ToListAsync(cancellationToken),
            searchHistory = await db.SearchHistoryEntries.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Label, x.QueryString, x.SearchedAt }).ToListAsync(cancellationToken),
            savedSearches = await db.SavedSearches.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.Name, x.QueryString, x.LastKnownCount, x.NotificationsEnabled, x.CreatedAt, x.LastCheckedAt }).ToListAsync(cancellationToken)
        };
        var monetization = new
        {
            entitlements = await db.Entitlements.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.PlanCode, x.ActiveListingLimit, x.StartsAt, x.EndsAt }).ToListAsync(cancellationToken),
            bonusLedger = await db.BonusLedgerEntries.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.Amount, x.Reason, x.PaymentId, x.CreatedAt, x.ExpiresAt }).ToListAsync(cancellationToken),
            payments = await db.PaymentTransactions.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.Kind, x.ProductType, x.ProductCode, x.ListingId, x.GrossAmount, x.DiscountAmount, x.BonusUsed, x.CashAmount, x.Status, x.ParentPaymentId, x.CreatedAt }).ToListAsync(cancellationToken),
            promotions = await db.PromotionPurchases.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.ListingId, x.ProductCode, x.PaymentId, x.StartsAt, x.EndsAt, x.Status }).ToListAsync(cancellationToken),
            refunds = await db.RefundRequests.AsNoTracking().Where(x => x.RequestedById == userId).Select(x => new { x.Id, x.PaymentId, x.Reason, x.Status, x.ResolutionNote, x.RequestedAt, x.ResolvedAt }).ToListAsync(cancellationToken)
        };

        var export = new { schemaVersion = 1, exportedAt = DateTimeOffset.UtcNow, profile, roles, sessions, audit, listings, conversations, engagement, recommendations, monetization };
        var json = JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return File(json, "application/json; charset=utf-8", $"marketplace-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
    }

    private AuditEvent UserAudit(Guid userId, string eventType, Guid requestId, string status) => new()
    {
        UserId = userId, EventType = eventType, Detail = $"Account erasure request {requestId}", EntityType = "AccountErasureRequest", EntityId = requestId.ToString(),
        NewValue = status, CorrelationId = HttpContext.TraceIdentifier, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
    };
}
