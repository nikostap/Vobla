using System.Text.Json;

namespace Marketplace.Web.Modules.Catalog;

public sealed class JsonListingCatalog(
    IWebHostEnvironment environment,
    ILogger<JsonListingCatalog> logger) : IListingCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly Lazy<Task<IReadOnlyList<ListingCard>>> _cache = new(() => LoadAsync(environment, logger));

    public Task<IReadOnlyList<ListingCard>> GetFeaturedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _cache.Value;
    }

    public async Task<CatalogSearchResult> SearchAsync(CatalogSearchRequest request, CancellationToken cancellationToken)
    {
        var source = await GetFeaturedAsync(cancellationToken);
        IEnumerable<ListingCard> query = source;
        if (!string.IsNullOrWhiteSpace(request.Query)) query = query.Where(x => $"{x.Title} {x.Description} {x.Category} {x.Location}".Contains(request.Query.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Category)) query = query.Where(x => x.Category.Contains(request.Category, StringComparison.OrdinalIgnoreCase));
        if (request.CenterLatitude is decimal centerLatitude && request.CenterLongitude is decimal centerLongitude)
        {
            query = query.Where(x => x.Coordinates.Count >= 2 && Modules.Geo.GeoPrivacy.DistanceKm((double)centerLatitude, (double)centerLongitude, x.Coordinates[0], x.Coordinates[1]) <= request.RadiusKm);
        }
        else if (!string.IsNullOrWhiteSpace(request.City) && request.City != "Россия")
        {
            query = query.Where(x => x.Location.Contains(request.City, StringComparison.OrdinalIgnoreCase));
            var center = Modules.Geo.GeoPrivacy.GetCityCenter(request.City);
            query = query.Where(x => x.Coordinates.Count >= 2 && Modules.Geo.GeoPrivacy.DistanceKm((double)center.Latitude, (double)center.Longitude, x.Coordinates[0], x.Coordinates[1]) <= request.RadiusKm);
        }
        if (request.MinPrice is not null) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice is not null) query = query.Where(x => x.Price <= request.MaxPrice);
        query = request.Sort switch
        {
            "price-asc" => query.OrderBy(x => x.PriceLabel == "Бесплатно" ? 0 : 1).ThenBy(x => x.PriceLabel == "Бесплатно" ? 0 : x.Price),
            "price-desc" => query.OrderByDescending(x => x.PriceLabel == "Бесплатно" ? 0 : x.Price),
            "newest" => query.OrderByDescending(x => x.PublishedLabel),
            _ => query
        };
        var allItems = query.ToList();
        var items = allItems.Skip(Math.Max(0, request.Offset)).Take(Math.Clamp(request.Limit, 1, 24)).ToList();
        return new CatalogSearchResult(items, allItems.Count, false);
    }

    private static async Task<IReadOnlyList<ListingCard>> LoadAsync(
        IWebHostEnvironment environment,
        ILogger logger)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "sample-listings.json");
        await using var stream = File.OpenRead(path);
        var listings = await JsonSerializer.DeserializeAsync<List<ListingCard>>(stream, SerializerOptions)
            ?? throw new InvalidOperationException("Demo listing seed is empty.");

        logger.LogInformation("Loaded {ListingCount} demo listings from {SeedPath}", listings.Count, path);
        return listings;
    }
}
