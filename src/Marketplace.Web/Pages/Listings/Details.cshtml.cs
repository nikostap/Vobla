using System.Security.Claims;
using System.Text.Json;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Moderation;
using Marketplace.Web.Modules.Messaging;
using Marketplace.Web.Modules.Engagement;
using Marketplace.Web.Modules.Recommendations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Listings;

public sealed class DetailsModel(MarketplaceDbContext db) : PageModel
{
    public sealed record MediaView(string Url, string Alt);
    public sealed record AttributeView(string Name, string Value, string? Unit);
    public sealed record PriceView(string Label, DateTimeOffset At);
    public sealed record RevisionView(int Number, DateTimeOffset At);
    public sealed record StatusView(string Status, DateTimeOffset At);
    public sealed record ModerationView(Guid CaseId, string Action, string Reason, string? ProblemField, string RuleReference, string Instruction, string RuleSetVersion, bool HasPendingAppeal);
    public sealed record ListingView(Guid Id, string Title, string? Description, string CategoryName, string CategorySlug, string StatusLabel, string PriceLabel, List<MediaView> Media, List<AttributeView> Attributes, List<PriceView> Prices, List<RevisionView> Revisions, List<StatusView> Statuses);
    public sealed record SellerView(Guid Id, string Name, string City, string? AvatarUrl, bool Verified, int ActiveListings, double? Rating, int ReviewCount);
    public sealed record RecommendationCard(Guid Id, string Title, string PriceLabel, string? Image, string? City);
    public ListingView? Listing { get; private set; }
    public bool IsOwner { get; private set; }
    public bool IsFavorite { get; private set; }
    public bool IsCompared { get; private set; }
    public string? PendingIntent { get; private set; }
    public SellerView? Seller { get; private set; }
    public ModerationView? Moderation { get; private set; }
    public IReadOnlyList<RecommendationCard> SimilarListings { get; private set; } = [];
    public IReadOnlyList<RecommendationCard> RecentlyViewed { get; private set; } = [];

    [BindProperty]
    public string AppealMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, string? intent)
    {
        var entity = await db.Listings.AsNoTracking().Include(x => x.Category).Include(x => x.Owner).Include(x => x.Media).Include(x => x.Location).Include(x => x.AttributeValues).Include(x => x.PriceHistory).Include(x => x.Revisions).Include(x => x.StatusHistory).FirstOrDefaultAsync(x => x.Id == id && x.Status != "Deleted");
        if (entity is null) return NotFound();
        IsOwner = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId == entity.OwnerId;
        if (userId != Guid.Empty) { IsFavorite = await db.Favorites.AnyAsync(x => x.UserId == userId && x.ListingId == id); IsCompared = await db.ComparisonItems.AnyAsync(x => x.UserId == userId && x.ListingId == id); }
        if (!IsOwner && entity.Status is not ("Active" or "PendingManualReview")) return NotFound();
        var definitions = await db.CategoryAttributes.AsNoTracking().Where(x => x.SchemaVersionId == entity.CategorySchemaVersionId).ToDictionaryAsync(x => x.Code);
        Listing = new ListingView(entity.Id, entity.Title, entity.Description, entity.Category.Name, entity.Category.Slug, entity.Status == "Draft" ? "Черновик" : entity.Status == "PendingManualReview" ? "На проверке" : entity.Status == "Quarantine" ? "Карантин" : entity.Status, entity.DealType == "Free" ? "Бесплатно" : entity.Price is null ? "Цена не указана" : $"{entity.Price:N0} ₽", entity.Media.OrderBy(x => x.SortOrder).Select(x => new MediaView(x.Url, x.Alt)).ToList(), entity.AttributeValues.Where(x => definitions.ContainsKey(x.AttributeCode)).Select(x => new AttributeView(definitions[x.AttributeCode].Name, JsonSerializer.Deserialize<string>(x.ValueJson) ?? string.Empty, definitions[x.AttributeCode].Unit)).ToList(), entity.PriceHistory.OrderByDescending(x => x.ChangedAt).Select(x => new PriceView(x.DealType == "Free" ? "Бесплатно" : x.Price is null ? "Цена не указана" : $"{x.Price:N0} ₽", x.ChangedAt)).ToList(), entity.Revisions.OrderByDescending(x => x.RevisionNumber).Select(x => new RevisionView(x.RevisionNumber, x.CreatedAt)).ToList(), entity.StatusHistory.OrderByDescending(x => x.CreatedAt).Select(x => new StatusView(x.Status, x.CreatedAt)).ToList());
        var sellerReviews = db.Reviews.AsNoTracking().Where(x => x.SubjectUserId == entity.OwnerId);
        var reviewCount = await sellerReviews.CountAsync();
        var rating = reviewCount == 0 ? (double?)null : await sellerReviews.AverageAsync(x => (double)x.Rating);
        var activeListings = await db.Listings.CountAsync(x => x.OwnerId == entity.OwnerId && x.Status == "Active");
        Seller = new SellerView(entity.OwnerId, entity.Owner.DisplayName, entity.Owner.City, entity.Owner.AvatarUrl, entity.Owner.EmailConfirmed, activeListings, rating, reviewCount);
        if (userId != Guid.Empty && !IsOwner && intent is "contact" or "favorite" or "compare") PendingIntent = intent;
        SimilarListings = await db.Listings.AsNoTracking().Include(x => x.Media).Include(x => x.Location)
            .Where(x => x.Status == "Active" && x.Id != id && x.CategoryId == entity.CategoryId)
            .OrderByDescending(x => entity.Location != null && x.Location != null && x.Location.City == entity.Location.City)
            .ThenByDescending(x => x.Condition == entity.Condition)
            .ThenBy(x => entity.Price == null || x.Price == null ? decimal.MaxValue : Math.Abs(x.Price.Value - entity.Price.Value))
            .ThenByDescending(x => x.Media.Any())
            .ThenByDescending(x => x.PublishedAt).Take(4)
            .Select(x => new RecommendationCard(x.Id, x.Title, x.DealType == "Free" ? "Бесплатно" : x.Price == null ? "Цена не указана" : $"{x.Price:N0} ₽", x.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(), x.Location == null ? null : x.Location.City)).ToListAsync();
        if (!IsOwner && userId != Guid.Empty)
        {
            var user = await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId);
            if (user.UseHistoryForRecommendations)
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
                await db.ListingViewEvents.Where(x => x.UserId == userId && x.ViewedAt < cutoff).ExecuteDeleteAsync();
                var storedView = await db.ListingViewEvents.FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == id);
                if (storedView is null) db.ListingViewEvents.Add(new ListingViewEvent { Id = Guid.NewGuid(), UserId = userId, ListingId = id });
                else storedView.ViewedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync();
                RecentlyViewed = await db.ListingViewEvents.AsNoTracking().Where(x => x.UserId == userId && x.ListingId != id && x.ViewedAt >= cutoff && x.Listing.Status == "Active")
                    .OrderByDescending(x => x.ViewedAt).Take(4)
                    .Select(x => new RecommendationCard(x.Listing.Id, x.Listing.Title, x.Listing.DealType == "Free" ? "Бесплатно" : x.Listing.Price == null ? "Цена не указана" : $"{x.Listing.Price:N0} ₽", x.Listing.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault(), x.Listing.Location == null ? null : x.Listing.Location.City)).ToListAsync();
            }
        }
        if (IsOwner)
        {
            var moderationCase = await db.ModerationCases.AsNoTracking()
                .Include(x => x.Decisions).Include(x => x.Appeals)
                .Where(x => x.ListingId == id && x.Decisions.Any())
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
            var decision = moderationCase?.Decisions.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            if (moderationCase is not null && decision is not null)
                Moderation = new ModerationView(moderationCase.Id, decision.Action, decision.Reason, decision.ProblemField, decision.RuleReference, decision.CorrectionInstruction, moderationCase.RuleSetVersion, moderationCase.Appeals.Any(x => x.Status == "Pending"));
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAppealAsync(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        var listing = await db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == userId);
        if (listing is null) return NotFound();
        var moderationCase = await db.ModerationCases.Include(x => x.Appeals)
            .Where(x => x.ListingId == id && x.Decisions.Any(d => d.Action == "Reject" || d.Action == "NeedsCorrection"))
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        var message = AppealMessage.Trim();
        if (moderationCase is null || message.Length is < 10 or > 2000 || moderationCase.Appeals.Any(x => x.Status == "Pending"))
            return RedirectToPage(new { id });
        db.ModerationAppeals.Add(new ModerationAppeal { Id = Guid.NewGuid(), ModerationCaseId = moderationCase.Id, UserId = userId, Message = message });
        moderationCase.Status = "Appealed";
        moderationCase.ResolvedAt = null;
        listing.Status = "Appealed";
        db.ListingStatusHistory.Add(new Modules.Listings.ListingStatusHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Status = "Appealed", Reason = "Пользователь подал апелляцию." });
        db.AuditEvents.Add(new AuditEvent { UserId = userId, EventType = "moderation.appeal", Detail = $"Appeal: {listing.Title}", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync();
        TempData["Notice"] = "Апелляция отправлена модератору.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartChatAsync(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var buyerId)) return Challenge();
        var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == "Active");
        if (listing is null) return NotFound();
        if (listing.OwnerId == buyerId) return RedirectToPage(new { id });
        var conversation = await db.Conversations.FirstOrDefaultAsync(x => x.ListingId == id && x.BuyerId == buyerId);
        if (conversation is null)
        {
            conversation = new Conversation { Id = Guid.NewGuid(), ListingId = id, SellerId = listing.OwnerId, BuyerId = buyerId };
            db.Conversations.Add(conversation);
            db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, Kind = "System", Text = "Диалог начат по объявлению." });
            var autoReply = await db.AutoReplySettings.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerId == listing.OwnerId && x.IsEnabled && (x.ListingId == null || x.ListingId == id));
            if (autoReply is not null) db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversation.Id, SenderId = listing.OwnerId, Kind = "AutoReply", Text = autoReply.Text, DeliveredAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        return RedirectToPage("/Messages/Chat", new { id = conversation.Id });
    }

    public async Task<IActionResult> OnPostFavoriteAsync(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status != "Deleted"); if (listing is null) return NotFound();
        var stored = await db.Favorites.FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == id); if (stored is null) db.Favorites.Add(new Favorite { Id = Guid.NewGuid(), UserId = userId, ListingId = id, PriceWhenAdded = listing.Price }); else db.Favorites.Remove(stored);
        await db.SaveChangesAsync();
        TempData["Notice"] = stored is null ? "Объявление добавлено в избранное." : "Объявление удалено из избранного.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCompareAsync(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status != "Deleted"); if (listing is null) return NotFound();
        var stored = await db.ComparisonItems.Include(x => x.Listing).FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == id);
        if (stored is not null) db.ComparisonItems.Remove(stored);
        else { var incompatible = await db.ComparisonItems.AnyAsync(x => x.UserId == userId && x.Listing.CategoryId != listing.CategoryId); if (incompatible) { TempData["Notice"] = "Сравнивать можно только объявления одной категории."; return RedirectToPage(new { id }); } db.ComparisonItems.Add(new ComparisonItem { Id = Guid.NewGuid(), UserId = userId, ListingId = id }); }
        await db.SaveChangesAsync();
        TempData["Notice"] = stored is null ? "Объявление добавлено к сравнению." : "Объявление удалено из сравнения.";
        return RedirectToPage(new { id });
    }
}
