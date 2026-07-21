namespace Marketplace.Web.Modules.Listings;
public sealed class ListingAttributeValue { public Guid Id { get; set; } public Guid ListingId { get; set; } public Listing Listing { get; set; } = null!; public string AttributeCode { get; set; } = string.Empty; public string ValueJson { get; set; } = string.Empty; }
