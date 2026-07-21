using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Marketplace.Web.Modules.Engagement;
using Marketplace.Web.Modules.Listings;
using Marketplace.Web.Modules.Storage;

namespace Marketplace.Web.Pages.Messages;

[Authorize]
public sealed class ChatModel(MarketplaceDbContext db, MessagingService messaging, IWebHostEnvironment environment, ILogger<ChatModel> logger) : PageModel
{
    public sealed record AttachmentView(Guid Id, string Name, string MimeType, long Size);
    public sealed record MessageView(Guid Id, Guid? SenderId, string Sender, string Kind, string Text, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, List<AttachmentView> Attachments);
    public sealed record ChatView(Guid Id, Guid ListingId, string ListingTitle, string Price, string Status, Guid SellerId, string SellerName, bool SellerVerified, string OtherName, bool IsSeller, bool IsBlocked, Guid? DealId, string? DealStatus, bool CanReview, bool HasReviewed, List<MessageView> Messages);
    public ChatView? Chat { get; private set; }
    public IReadOnlyList<ReplyTemplate> Templates { get; private set; } = [];
    [BindProperty] public string Text { get; set; } = string.Empty;
    [BindProperty] public List<IFormFile> Files { get; set; } = [];
    [TempData] public string? Notice { get; set; }
    [TempData] public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id) { if (!await LoadAsync(id)) return NotFound(); return Page(); }

    public async Task<IActionResult> OnPostSendAsync(Guid id)
    {
        var userId = CurrentUserId();
        try
        {
            if (Files.Count > 0) await SendAttachmentsAsync(id, userId);
            else await messaging.SendAsync(id, userId, Text);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException) { Error = exception.Message; }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUploadAsync(Guid id)
    {
        try { await SendAttachmentsAsync(id, CurrentUserId()); }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException) { Error = exception.Message; }
        return RedirectToPage(new { id });
    }

    private async Task SendAttachmentsAsync(Guid id, Guid userId)
    {
        var valid = new List<IFormFile>();
        foreach (var file in Files.Take(3))
        {
            var metadataAllowed = file.Length > 0 && file.Length <= MessagingPolicy.MaxAttachmentBytes && MessagingPolicy.AllowedFiles.TryGetValue(file.ContentType, out var extensions) && extensions.Contains(Path.GetExtension(file.FileName), StringComparer.OrdinalIgnoreCase);
            if (metadataAllowed && await UploadContentValidator.MatchesDeclaredTypeAsync(file, HttpContext.RequestAborted)) valid.Add(file);
        }
        if (valid.Count != Files.Count || valid.Count == 0) throw new InvalidOperationException("Файл отклонён: до 20 МБ, разрешены JPG, PNG, WebP, PDF и TXT, не более трёх файлов.");
        var storedFiles = new List<StoredUpload>();
        await using var transaction = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            var dto = await messaging.SendAsync(id, userId, string.IsNullOrWhiteSpace(Text) ? $"Вложение: {string.Join(", ", valid.Select(x => Path.GetFileName(x.FileName)))}" : Text, "Attachment", HttpContext.RequestAborted);
            var relativeDirectory = id.ToString("N");
            var directory = Path.Combine(environment.ContentRootPath, "App_Data", "chat", relativeDirectory);
            foreach (var file in valid)
            {
                var stored = await AtomicUploadStorage.SaveAsync(file, directory, Path.GetExtension(file.FileName), HttpContext.RequestAborted);
                storedFiles.Add(stored);
                db.ChatAttachments.Add(new ChatAttachment { Id = Guid.NewGuid(), MessageId = dto.Id, OriginalName = Path.GetFileName(file.FileName), StorageKey = Path.Combine(relativeDirectory, stored.FileName), MimeType = file.ContentType, Size = file.Length });
            }
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            AtomicUploadStorage.Cleanup(storedFiles);
            throw;
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            AtomicUploadStorage.Cleanup(storedFiles);
            throw;
        }
        catch (Exception exception)
        {
            AtomicUploadStorage.Cleanup(storedFiles);
            logger.LogError(exception, "Attachment upload failed for conversation {ConversationId}; stored files were compensated.", id);
            throw new InvalidOperationException("Вложение не удалось сохранить. Попробуйте ещё раз.", exception);
        }
        Notice = valid.Count == 1 ? "Файл отправлен вместе с сообщением." : $"Файлы отправлены вместе с сообщением: {valid.Count}.";
    }

    public async Task<IActionResult> OnGetAttachmentAsync(Guid attachmentId)
    {
        var userId = CurrentUserId(); var attachment = await db.ChatAttachments.AsNoTracking().Include(x => x.Message).ThenInclude(x => x.Conversation).FirstOrDefaultAsync(x => x.Id == attachmentId);
        if (attachment is null || (attachment.Message.Conversation.SellerId != userId && attachment.Message.Conversation.BuyerId != userId)) return NotFound();
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "chat", attachment.StorageKey)); var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "App_Data", "chat")); if (!path.StartsWith(root, StringComparison.Ordinal) || !System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, attachment.MimeType, attachment.OriginalName);
    }

    public async Task<IActionResult> OnPostBlockAsync(Guid id) { var userId = CurrentUserId(); var chat = await db.Conversations.FirstOrDefaultAsync(x => x.Id == id && (x.SellerId == userId || x.BuyerId == userId)); if (chat is null) return NotFound(); var other = chat.SellerId == userId ? chat.BuyerId : chat.SellerId; if (!await db.ChatBlocks.AnyAsync(x => x.ConversationId == id && x.BlockerId == userId)) db.ChatBlocks.Add(new ChatBlock { Id = Guid.NewGuid(), ConversationId = id, BlockerId = userId, BlockedId = other }); await db.SaveChangesAsync(); Notice = "Пользователь заблокирован в этом диалоге."; return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostReportAsync(Guid id, Guid? messageId, string reason) { var userId = CurrentUserId(); if (!await db.Conversations.AnyAsync(x => x.Id == id && (x.SellerId == userId || x.BuyerId == userId))) return NotFound(); db.ChatReports.Add(new ChatReport { Id = Guid.NewGuid(), ConversationId = id, MessageId = messageId, ReporterId = userId, Reason = string.IsNullOrWhiteSpace(reason) ? "Нарушение правил чата" : reason.Trim() }); await db.SaveChangesAsync(); Notice = "Жалоба отправлена на проверку."; return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostSaveTemplateAsync(Guid id, string title, string templateText) { var userId = CurrentUserId(); if (!string.IsNullOrWhiteSpace(templateText)) { db.ReplyTemplates.Add(new ReplyTemplate { Id = Guid.NewGuid(), OwnerId = userId, Title = string.IsNullOrWhiteSpace(title) ? "Быстрый ответ" : title.Trim(), Text = templateText.Trim() }); await db.SaveChangesAsync(); } return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostAutoReplyAsync(Guid id, bool enabled, string autoReplyText) { var userId = CurrentUserId(); var chat = await db.Conversations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId); if (chat is null) return NotFound(); var setting = await db.AutoReplySettings.FirstOrDefaultAsync(x => x.OwnerId == userId && x.ListingId == chat.ListingId); if (setting is null) db.AutoReplySettings.Add(new AutoReplySetting { Id = Guid.NewGuid(), OwnerId = userId, ListingId = chat.ListingId, IsEnabled = enabled, Text = autoReplyText.Trim() }); else { setting.IsEnabled = enabled; setting.Text = autoReplyText.Trim(); setting.UpdatedAt = DateTimeOffset.UtcNow; } await db.SaveChangesAsync(); Notice = "Автоответ сохранён."; return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostRequestDealAsync(Guid id) { var userId = CurrentUserId(); var chat = await db.Conversations.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == id && x.BuyerId == userId); if (chat is null || chat.Listing.Status != "Active") return NotFound(); if (!await db.Deals.AnyAsync(x => x.ConversationId == id)) { db.Deals.Add(new Deal { Id = Guid.NewGuid(), ConversationId = id, ListingId = chat.ListingId, SellerId = chat.SellerId, BuyerId = chat.BuyerId }); db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = id, Kind = "System", Text = "Покупатель сообщил о покупке. Ожидается подтверждение продавца." }); db.UserNotifications.Add(new UserNotification { Id = Guid.NewGuid(), UserId = chat.SellerId, Type = "DealRequested", Text = $"Покупатель просит подтвердить сделку: {chat.Listing.Title}", Link = $"/Messages/Chat?id={id}" }); await db.SaveChangesAsync(); } return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostConfirmDealAsync(Guid id) { var userId = CurrentUserId(); var deal = await db.Deals.Include(x => x.Listing).FirstOrDefaultAsync(x => x.ConversationId == id && x.SellerId == userId && x.Status == "Requested"); if (deal is null) return NotFound(); deal.Status = "Confirmed"; deal.ConfirmedAt = DateTimeOffset.UtcNow; deal.Listing.Status = "Completed"; db.ListingStatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = deal.ListingId, Status = "Completed", Reason = "Сделка подтверждена продавцом." }); db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), ConversationId = id, Kind = "System", Text = "Сделка подтверждена сторонами. Теперь можно оставить отзыв." }); db.UserNotifications.Add(new UserNotification { Id = Guid.NewGuid(), UserId = deal.BuyerId, Type = "DealConfirmed", Text = $"Сделка подтверждена: {deal.Listing.Title}", Link = $"/Messages/Chat?id={id}" }); await db.SaveChangesAsync(); return RedirectToPage(new { id }); }
    public async Task<IActionResult> OnPostReviewAsync(Guid id, int rating, string reviewText) { var userId = CurrentUserId(); var deal = await db.Deals.FirstOrDefaultAsync(x => x.ConversationId == id && x.Status == "Confirmed" && (x.SellerId == userId || x.BuyerId == userId)); if (deal is null || rating is < 1 or > 5 || await db.Reviews.AnyAsync(x => x.DealId == deal.Id && x.AuthorId == userId)) return RedirectToPage(new { id }); var subject = deal.SellerId == userId ? deal.BuyerId : deal.SellerId; db.Reviews.Add(new Review { Id = Guid.NewGuid(), DealId = deal.Id, AuthorId = userId, SubjectUserId = subject, Rating = rating, Text = reviewText.Trim() }); await db.SaveChangesAsync(); Notice = "Отзыв опубликован как подтверждённый сделкой."; return RedirectToPage(new { id }); }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<bool> LoadAsync(Guid id)
    {
        var userId = CurrentUserId(); var item = await db.Conversations.Include(x => x.Listing).Include(x => x.Seller).Include(x => x.Buyer).Include(x => x.Messages).ThenInclude(x => x.Sender).Include(x => x.Messages).ThenInclude(x => x.Attachments).FirstOrDefaultAsync(x => x.Id == id && (x.SellerId == userId || x.BuyerId == userId)); if (item is null) return false;
        await messaging.MarkReadAsync(id, userId); var other = item.SellerId == userId ? item.Buyer : item.Seller; var blocked = await db.ChatBlocks.AnyAsync(x => x.ConversationId == id); Templates = await db.ReplyTemplates.AsNoTracking().Where(x => x.OwnerId == userId).OrderBy(x => x.Title).ToListAsync(); var deal = await db.Deals.AsNoTracking().FirstOrDefaultAsync(x => x.ConversationId == id); var hasReviewed = deal is not null && await db.Reviews.AnyAsync(x => x.DealId == deal.Id && x.AuthorId == userId);
        Chat = new ChatView(item.Id, item.ListingId, item.Listing.Title, item.Listing.DealType == "Free" ? "Бесплатно" : item.Listing.Price is null ? "Цена не указана" : $"{item.Listing.Price:N0} ₽", item.Listing.Status, item.SellerId, item.Seller.DisplayName, item.Seller.EmailConfirmed, other.DisplayName, item.SellerId == userId, blocked, deal?.Id, deal?.Status, deal?.Status == "Confirmed", hasReviewed, item.Messages.OrderBy(x => x.CreatedAt).Select(x => new MessageView(x.Id, x.SenderId, x.Sender?.DisplayName ?? "Система", x.Kind, x.Text, x.CreatedAt, x.ReadAt, x.Attachments.Select(a => new AttachmentView(a.Id, a.OriginalName, a.MimeType, a.Size)).ToList())).ToList()); return true;
    }
}
