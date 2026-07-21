namespace Marketplace.Web.Modules.Identity;

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? ActorRole { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
