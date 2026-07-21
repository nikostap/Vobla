namespace Marketplace.Web.Modules.Messaging;

public sealed class ChatBlock { public Guid Id { get; set; } public Guid ConversationId { get; set; } public Guid BlockerId { get; set; } public Guid BlockedId { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class ChatReport { public Guid Id { get; set; } public Guid ConversationId { get; set; } public Guid? MessageId { get; set; } public Guid ReporterId { get; set; } public string Reason { get; set; } = string.Empty; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class ReplyTemplate { public Guid Id { get; set; } public Guid OwnerId { get; set; } public string Title { get; set; } = string.Empty; public string Text { get; set; } = string.Empty; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; }
public sealed class AutoReplySetting { public Guid Id { get; set; } public Guid OwnerId { get; set; } public Guid? ListingId { get; set; } public bool IsEnabled { get; set; } public string Text { get; set; } = string.Empty; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; }

public static class MessagingPolicy
{
    public const long MaxAttachmentBytes = 20 * 1024 * 1024;
    public static readonly IReadOnlyDictionary<string, string[]> AllowedFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"], ["image/png"] = [".png"], ["image/webp"] = [".webp"], ["application/pdf"] = [".pdf"], ["text/plain"] = [".txt"]
    };
    public static bool ContainsForbiddenLink(string text) => text.Contains("http://", StringComparison.OrdinalIgnoreCase) || text.Contains("https://", StringComparison.OrdinalIgnoreCase) || text.Contains("t.me/", StringComparison.OrdinalIgnoreCase);
}
