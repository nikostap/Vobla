namespace Marketplace.Web.Modules.Catalog;

public sealed record CatalogSearchRequest(
    string? Query = null,
    string? Category = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Condition = null,
    string? DealType = null,
    bool HasPhoto = false,
    string Sort = "recommended",
    IReadOnlyDictionary<string, string>? Attributes = null,
    string? City = null,
    int RadiusKm = 30,
    int Offset = 0,
    int Limit = 12,
    decimal? CenterLatitude = null,
    decimal? CenterLongitude = null)
{
    public bool HasCriteria => !string.IsNullOrWhiteSpace(Query) || !string.IsNullOrWhiteSpace(Category) || MinPrice is not null || MaxPrice is not null || !string.IsNullOrWhiteSpace(Condition) || !string.IsNullOrWhiteSpace(DealType) || HasPhoto || Attributes?.Count > 0;
}

public sealed record CatalogSearchResult(IReadOnlyList<ListingCard> Items, int Total, bool FromDatabase, string? CorrectedQuery = null);
