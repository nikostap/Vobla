using Marketplace.Web.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Marketplace.Web.Modules.Geo;
using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Catalog;

public sealed class DatabaseListingCatalog(MarketplaceDbContext db, JsonListingCatalog fallback, IWebHostEnvironment environment) : IListingCatalog
{
    public Task<IReadOnlyList<ListingCard>> GetFeaturedAsync(CancellationToken cancellationToken) => fallback.GetFeaturedAsync(cancellationToken);

    public async Task<CatalogSearchResult> SearchAsync(CatalogSearchRequest request, CancellationToken cancellationToken)
    {
        var query = db.Listings.AsNoTracking().Where(x => x.Status == "Active");
        if (!request.HasCriteria && environment.IsDevelopment())
        {
            var featuredIds = DevelopmentListingSeed.FeaturedIds;
            query = query.Where(x => featuredIds.Contains(x.Id));
        }
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim();
            var pattern = $"%{term}%";
            query = query.Where(x =>
                EF.Functions.ToTsVector("russian", x.Title + " " + (x.Description ?? "")).Matches(EF.Functions.PlainToTsQuery("russian", term)) ||
                EF.Functions.ILike(x.Title, pattern) || EF.Functions.ILike(x.Description ?? "", pattern) || EF.Functions.ILike(x.Category.Name, pattern) ||
                EF.Functions.TrigramsWordSimilarity(term, x.Title) >= .3);
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var categoryTree = await db.CatalogCategories.AsNoTracking().Select(x => new { x.Id, x.ParentId, x.Slug }).ToListAsync(cancellationToken);
            var selected = categoryTree.FirstOrDefault(x => x.Slug == request.Category);
            if (selected is null) return new CatalogSearchResult([], 0, true);
            var categoryIds = new HashSet<Guid> { selected.Id };
            var frontier = new Queue<Guid>();
            frontier.Enqueue(selected.Id);
            while (frontier.TryDequeue(out var parentId))
            {
                foreach (var child in categoryTree.Where(x => x.ParentId == parentId && categoryIds.Add(x.Id))) frontier.Enqueue(child.Id);
            }
            query = query.Where(x => categoryIds.Contains(x.CategoryId));
        }
        if (request.CenterLatitude is decimal centerLatitude && request.CenterLongitude is decimal centerLongitude)
        {
            var latitudeDelta = request.RadiusKm / 111m;
            var longitudeScale = Math.Max(.2m, (decimal)Math.Cos((double)centerLatitude * Math.PI / 180d));
            var longitudeDelta = request.RadiusKm / (111m * longitudeScale);
            query = query.Where(x => x.Location != null &&
                x.Location.PublicLatitude >= centerLatitude - latitudeDelta && x.Location.PublicLatitude <= centerLatitude + latitudeDelta &&
                x.Location.PublicLongitude >= centerLongitude - longitudeDelta && x.Location.PublicLongitude <= centerLongitude + longitudeDelta);
        }
        else if (!string.IsNullOrWhiteSpace(request.City) && request.City != "Россия") query = query.Where(x => x.Location != null && x.Location.City == request.City);
        if (request.MinPrice is not null) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice is not null) query = query.Where(x => x.Price <= request.MaxPrice);
        if (!string.IsNullOrWhiteSpace(request.Condition)) query = query.Where(x => x.Condition == request.Condition);
        if (!string.IsNullOrWhiteSpace(request.DealType)) query = query.Where(x => x.DealType == request.DealType);
        if (request.HasPhoto) query = query.Where(x => x.Media.Any());
        foreach (var attribute in request.Attributes ?? new Dictionary<string, string>())
        {
            var code = attribute.Key;
            var value = attribute.Value;
            query = query.Where(x => x.AttributeValues.Any(a => a.AttributeCode == code && EF.Functions.ILike(a.ValueJson, $"%{value}%")));
        }
        query = request.Sort switch
        {
            "price-asc" => query
                .OrderBy(x => x.DealType == "Free" ? 0 : x.Price == null ? 2 : 1)
                .ThenBy(x => x.DealType == "Free" ? 0 : x.Price)
                .ThenByDescending(x => x.PublishedAt),
            "price-desc" => query
                .OrderBy(x => x.DealType != "Free" && x.Price == null ? 1 : 0)
                .ThenByDescending(x => x.DealType == "Free" ? 0 : x.Price)
                .ThenByDescending(x => x.PublishedAt),
            "newest" => query.OrderByDescending(x => x.PublishedAt),
            _ when !string.IsNullOrWhiteSpace(request.Query) => query.OrderByDescending(x =>
                EF.Functions.ToTsVector("russian", x.Title + " " + (x.Description ?? "")).Rank(EF.Functions.PlainToTsQuery("russian", request.Query.Trim())) +
                EF.Functions.TrigramsWordSimilarity(request.Query.Trim(), x.Title)).ThenByDescending(x => x.PublishedAt),
            _ => query.OrderByDescending(x => x.PublishedAt)
        };

        int total;
        List<Listing> entities;
        if (request.CenterLatitude is decimal exactLatitude && request.CenterLongitude is decimal exactLongitude)
        {
            var nearby = await query.Include(x => x.Category).Include(x => x.Owner).Include(x => x.Media).Include(x => x.Location).ToListAsync(cancellationToken);
            nearby = nearby.Where(x => x.Location is not null && GeoPrivacy.DistanceKm((double)exactLatitude, (double)exactLongitude, (double)x.Location.PublicLatitude, (double)x.Location.PublicLongitude) <= request.RadiusKm).ToList();
            total = nearby.Count;
            entities = nearby.Skip(Math.Max(0, request.Offset)).Take(Math.Clamp(request.Limit, 1, 24)).ToList();
        }
        else
        {
            total = await query.CountAsync(cancellationToken);
            entities = await query.Include(x => x.Category).Include(x => x.Owner).Include(x => x.Media).Include(x => x.Location)
                .Skip(Math.Max(0, request.Offset)).Take(Math.Clamp(request.Limit, 1, 24)).ToListAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.City) && request.City != "Россия") { var center = GeoPrivacy.GetCityCenter(request.City); entities = entities.Where(x => { var point = x.Location is null ? GeoPrivacy.CreateStablePublicPoint(x.Id, x.Owner.City) : (x.Location.PublicLatitude, x.Location.PublicLongitude); return GeoPrivacy.DistanceKm((double)center.Latitude, (double)center.Longitude, (double)point.Item1, (double)point.Item2) <= request.RadiusKm; }).ToList(); }
        }
        if (entities.Count == 0 && !request.HasCriteria && environment.IsDevelopment()) return await fallback.SearchAsync(request, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var items = entities.Select(x => { var point = x.Location is null ? GeoPrivacy.CreateStablePublicPoint(x.Id, x.Owner.City) : (x.Location.PublicLatitude, x.Location.PublicLongitude); return new ListingCard(
            x.Id.ToString(), x.Category.Name, x.Title, x.Price ?? 0,
            x.DealType == "Free" ? "Бесплатно" : x.Price is null ? "Цена не указана" : $"{x.Price:N0} ₽",
            x.Description ?? "Описание не добавлено", x.Location?.City ?? x.Owner.City,
            x.PublishedAt is null ? "Недавно" : now - x.PublishedAt < TimeSpan.FromDays(1) ? "Сегодня" : x.PublishedAt.Value.ToLocalTime().ToString("dd.MM.yyyy"),
            x.Owner.EmailConfirmed, false,
            x.Media.OrderBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault() ?? "/demo/images/placeholder.svg",
            [(double)point.Item1, (double)point.Item2]); }).ToList();
        return new CatalogSearchResult(items, total, true);
    }
}
