using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Messaging;

public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public Guid SellerId { get; set; }
    public ApplicationUser Seller { get; set; } = null!;
    public Guid BuyerId { get; set; }
    public ApplicationUser Buyer { get; set; } = null!;
    public bool IsArchivedBySeller { get; set; }
    public bool IsArchivedByBuyer { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid? SenderId { get; set; }
    public ApplicationUser? Sender { get; set; }
    public string Kind { get; set; } = "Text";
    public string Text { get; set; } = string.Empty;
    public Guid? ReplyToMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public List<ChatAttachment> Attachments { get; set; } = [];
}

public sealed class ChatAttachment
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public ChatMessage Message { get; set; } = null!;
    public string OriginalName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public string SafetyStatus { get; set; } = "Accepted";
}
