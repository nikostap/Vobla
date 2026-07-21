using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Geo;

namespace Marketplace.Web.Modules.Listings;

public sealed class Listing
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public CatalogCategory Category { get; set; } = null!;
    public Guid? CategorySchemaVersionId { get; set; }
    public CategorySchemaVersion? CategorySchemaVersion { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DealType { get; set; } = "FixedPrice";
    public decimal? Price { get; set; }
    public string Condition { get; set; } = "Used";
    public string Status { get; set; } = "Draft";
    public bool AllowMessages { get; set; } = true;
    public bool ShowPhone { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? RecreatedFromListingId { get; set; }
    public List<ListingRevision> Revisions { get; set; } = [];
    public List<ListingMedia> Media { get; set; } = [];
    public List<ListingPriceHistory> PriceHistory { get; set; } = [];
    public List<ListingAttributeValue> AttributeValues { get; set; } = [];
    public List<ListingStatusHistory> StatusHistory { get; set; } = [];
    public ListingLocation? Location { get; set; }
}
