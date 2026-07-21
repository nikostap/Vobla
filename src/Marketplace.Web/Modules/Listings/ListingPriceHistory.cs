namespace Marketplace.Web.Modules.Listings;
public sealed class ListingPriceHistory { public Guid Id { get; set; } public Guid ListingId { get; set; } public Listing Listing { get; set; } = null!; public decimal? Price { get; set; } public string DealType { get; set; } = string.Empty; public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow; }
