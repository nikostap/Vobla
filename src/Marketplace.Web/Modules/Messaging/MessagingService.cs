using Marketplace.Web.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Messaging;

public sealed record ChatMessageDto(Guid Id, Guid ConversationId, Guid? SenderId, string SenderName, string Kind, string Text, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);

public sealed class MessagingService(MarketplaceDbContext db)
{
    public async Task<ChatMessageDto> SendAsync(Guid conversationId, Guid senderId, string text, string kind = "Text", CancellationToken cancellationToken = default)
    {
        text = text.Trim();
        if (text.Length is < 1 or > 5000) throw new InvalidOperationException("Сообщение должно содержать от 1 до 5000 символов.");
        if (kind == "Text" && MessagingPolicy.ContainsForbiddenLink(text)) throw new InvalidOperationException("Внешние ссылки в чате запрещены.");
        var conversation = await db.Conversations.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == conversationId, cancellationToken) ?? throw new InvalidOperationException("Диалог не найден.");
        if (conversation.SellerId != senderId && conversation.BuyerId != senderId) throw new UnauthorizedAccessException();
        var otherId = conversation.SellerId == senderId ? conversation.BuyerId : conversation.SellerId;
        if (await db.ChatBlocks.AnyAsync(x => x.ConversationId == conversationId && (x.BlockerId == senderId || x.BlockerId == otherId), cancellationToken)) throw new InvalidOperationException("Отправка сообщений в этом диалоге заблокирована.");
        var senderName = await db.Users.Where(x => x.Id == senderId).Select(x => x.DisplayName).FirstAsync(cancellationToken);
        var message = new ChatMessage { Id = Guid.NewGuid(), ConversationId = conversationId, SenderId = senderId, Text = text, Kind = kind, DeliveredAt = DateTimeOffset.UtcNow };
        conversation.UpdatedAt = message.CreatedAt;
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return new ChatMessageDto(message.Id, conversationId, senderId, senderName, kind, text, message.CreatedAt, null);
    }

    public async Task MarkReadAsync(Guid conversationId, Guid readerId, CancellationToken cancellationToken = default)
    {
        var allowed = await db.Conversations.AnyAsync(x => x.Id == conversationId && (x.SellerId == readerId || x.BuyerId == readerId), cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException();
        var unread = await db.ChatMessages.Where(x => x.ConversationId == conversationId && x.SenderId != readerId && x.ReadAt == null).ToListAsync(cancellationToken);
        foreach (var item in unread) item.ReadAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
