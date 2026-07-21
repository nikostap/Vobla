using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Recommendations;

public sealed class ListingViewEvent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public DateTimeOffset ViewedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SearchHistoryEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public DateTimeOffset SearchedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SavedSearch
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string QueryString { get; set; } = string.Empty;
    public int LastKnownCount { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastCheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
