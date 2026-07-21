namespace Marketplace.Web.Modules.Administration;

public sealed class FeatureFlag
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public Guid? UpdatedById { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BackgroundJobRun
{
    public Guid Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public string Detail { get; set; } = string.Empty;
    public Guid? StartedById { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LockedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
