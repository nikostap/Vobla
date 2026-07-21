using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Geo;

public sealed class ListingLocation
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public string City { get; set; } = "Москва";
    public string? District { get; set; }
    public string? ExactAddress { get; set; }
    public decimal? ExactLatitude { get; set; }
    public decimal? ExactLongitude { get; set; }
    public decimal PublicLatitude { get; set; }
    public decimal PublicLongitude { get; set; }
    public bool IsExactPointPublic { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
