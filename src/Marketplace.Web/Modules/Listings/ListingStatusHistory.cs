namespace Marketplace.Web.Modules.Listings;
public sealed class ListingStatusHistory { public Guid Id { get; set; } public Guid ListingId { get; set; } public Listing Listing { get; set; } = null!; public string Status { get; set; } = string.Empty; public string? Reason { get; set; } public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
