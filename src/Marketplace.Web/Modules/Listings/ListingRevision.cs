namespace Marketplace.Web.Modules.Listings;
public sealed class ListingRevision { public Guid Id { get; set; } public Guid ListingId { get; set; } public Listing Listing { get; set; } = null!; public int RevisionNumber { get; set; } public string SnapshotJson { get; set; } = "{}"; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
