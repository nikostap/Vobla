namespace Marketplace.Web.Modules.Listings;
public sealed class ListingMedia { public Guid Id { get; set; } public Guid ListingId { get; set; } public Listing Listing { get; set; } = null!; public string Url { get; set; } = string.Empty; public string Alt { get; set; } = string.Empty; public int SortOrder { get; set; } public bool IsPrimary { get; set; } }
