using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Listings;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Geo;

public static class MarketplaceMapApi
{
    private const string ClientName = "maps-service";

    public static IServiceCollection AddMarketplaceMaps(this IServiceCollection services, IConfiguration configuration)
    {
        var configuredUrl = Environment.GetEnvironmentVariable("MAPS_API_BASE_URL") ?? configuration["Maps:ApiBaseUrl"];
        services.AddHttpClient(ClientName, client =>
        {
            if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var baseUri)) client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        return services;
    }

    public static IEndpointRouteBuilder MapMarketplaceMaps(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/maps-api/api/v1/map/config", GetConfigAsync);
        endpoints.MapGet("/maps-api/api/v1/map/health", ProxyHealthAsync);
        endpoints.MapGet("/maps-api/api/v1/geo/suggest", ProxySuggestionsAsync).RequireRateLimiting("geocoder");
        endpoints.MapGet("/maps-api/api/v1/map/clusters", GetClustersAsync);
        endpoints.MapGet("/maps-api/map-assets/{**path}", ServeOrProxyAssetAsync);
        endpoints.MapGet("/maps-api/vendor/{**path}", (HttpContext context, string? path, IHttpClientFactory clients) => ProxyAssetAsync(context, clients, $"/vendor/{path ?? string.Empty}"));
        return endpoints;
    }

    private static async Task<IResult> GetConfigAsync(IHttpClientFactory clients, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(ClientName);
        if (client.BaseAddress is null) return Results.Ok(DisabledConfig("MAPS_API_BASE_URL не настроен"));
        try
        {
            using var response = await client.GetAsync("/api/v1/map/config", cancellationToken);
            if (!response.IsSuccessStatusCode) return Results.Ok(DisabledConfig($"Сервис карты ответил {response.StatusCode}"));
            var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?.AsObject();
            if (node is null) return Results.Ok(DisabledConfig("Некорректная конфигурация карты"));
            node["basemapUrl"] = RewriteAssetUrl(node["basemapUrl"]?.GetValue<string>());
            node["assetsBaseUrl"] = "/maps-api/map-assets";
            if (node["styles"] is JsonObject styles)
            {
                foreach (var key in styles.Select(x => x.Key).ToArray()) styles[key] = RewriteAssetUrl(styles[key]?.GetValue<string>());
            }
            return Results.Json(node);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return Results.Ok(DisabledConfig("Сервис карты временно недоступен"));
        }
    }

    private static object DisabledConfig(string reason) => new
    {
        enabled = false,
        reason,
        basemapUrl = string.Empty,
        assetsBaseUrl = "/maps-api/map-assets",
        defaultView = new { latitude = 55.7558, longitude = 37.6176, zoom = 10 },
        zoom = new { min = 2, max = 18 },
        attributionHtml = "© OpenStreetMap contributors"
    };

    private static string RewriteAssetUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)) value = absolute.PathAndQuery;
        if (value.StartsWith("/map-assets", StringComparison.Ordinal)) return "/maps-api" + value;
        return value;
    }

    private static async Task<IResult> ProxyHealthAsync(IHttpClientFactory clients, CancellationToken cancellationToken)
    {
        var client = clients.CreateClient(ClientName);
        if (client.BaseAddress is null) return Results.Problem("MAPS_API_BASE_URL не настроен", statusCode: 503);
        try
        {
            using var response = await client.GetAsync("/api/v1/map/health", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.Text(body, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException) { return Results.Problem("Сервис карты недоступен", statusCode: 503); }
    }

    private static async Task<IResult> ProxySuggestionsAsync(string? q, string? cityId, IHttpClientFactory clients, CancellationToken cancellationToken)
    {
        q = q?.Trim();
        if (q is null || q.Length < 3 || q.Length > 200) return Results.BadRequest(new { error = "Введите от 3 до 200 символов" });
        var client = clients.CreateClient(ClientName);
        if (client.BaseAddress is null) return Results.Problem("Сервис подсказок не настроен", statusCode: 503);
        var url = $"/api/v1/geo/suggest?q={Uri.EscapeDataString(q)}" + (string.IsNullOrWhiteSpace(cityId) ? string.Empty : $"&cityId={Uri.EscapeDataString(cityId)}");
        try
        {
            using var response = await client.GetAsync(url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.Text(body, response.Content.Headers.ContentType?.ToString() ?? "application/json", statusCode: (int)response.StatusCode);
        }
        catch (HttpRequestException) { return Results.Problem("Сервис подсказок недоступен", statusCode: 503); }
    }

    private static async Task<IResult> GetClustersAsync(
        decimal west, decimal south, decimal east, decimal north, double zoom,
        string? query, string? category, decimal? priceFrom, decimal? priceTo,
        string? condition, string? dealType, bool? hasPhoto,
        MarketplaceDbContext db, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        if (west is < -180 or > 180 || east is < -180 or > 180 || south is < -90 or > 90 || north is < -90 or > 90 || west >= east || south >= north)
            return Results.BadRequest(new { error = "Некорректная область карты" });
        zoom = Math.Clamp(zoom, 2, 18);
        var listings = db.Listings.AsNoTracking().Where(x => x.Status == "Active" && x.Location != null &&
            x.Location.PublicLongitude >= west && x.Location.PublicLongitude <= east &&
            x.Location.PublicLatitude >= south && x.Location.PublicLatitude <= north);
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(category) && priceFrom is null && priceTo is null && string.IsNullOrWhiteSpace(condition) && string.IsNullOrWhiteSpace(dealType) && hasPhoto != true)
        {
            var featuredIds = DevelopmentListingSeed.FeaturedIds;
            listings = listings.Where(x => featuredIds.Contains(x.Id));
        }
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            if (term.Length > 200) return Results.BadRequest(new { error = "Слишком длинный поисковый запрос" });
            listings = listings.Where(x => EF.Functions.ILike(x.Title, $"%{term}%") || EF.Functions.ILike(x.Description ?? "", $"%{term}%"));
        }
        if (priceFrom is not null) listings = listings.Where(x => x.Price >= priceFrom);
        if (priceTo is not null) listings = listings.Where(x => x.Price <= priceTo);
        if (!string.IsNullOrWhiteSpace(condition)) listings = listings.Where(x => x.Condition == condition);
        if (!string.IsNullOrWhiteSpace(dealType)) listings = listings.Where(x => x.DealType == dealType);
        if (hasPhoto == true) listings = listings.Where(x => x.Media.Any());
        if (!string.IsNullOrWhiteSpace(category))
        {
            var tree = await db.CatalogCategories.AsNoTracking().Select(x => new { x.Id, x.ParentId, x.Slug }).ToListAsync(cancellationToken);
            var selected = tree.FirstOrDefault(x => x.Slug == category);
            if (selected is null) return Results.Ok(new { clusters = Array.Empty<object>(), items = Array.Empty<object>() });
            var ids = new HashSet<Guid> { selected.Id };
            while (true)
            {
                var children = tree.Where(x => x.ParentId is Guid parent && ids.Contains(parent) && ids.Add(x.Id)).ToList();
                if (children.Count == 0) break;
            }
            listings = listings.Where(x => ids.Contains(x.CategoryId));
        }

        var rows = await listings.OrderByDescending(x => x.PublishedAt).Take(5000).Select(x => new MapRow(
            x.Id, x.Title, x.Price, x.DealType, x.Location!.PublicLatitude, x.Location.PublicLongitude,
            x.Location.City, x.Category.Name, x.Owner.EmailConfirmed,
            x.Media.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.SortOrder).Select(m => m.Url).FirstOrDefault()
        )).ToListAsync(cancellationToken);
        var cellSize = CellSize(zoom);
        var groups = rows.GroupBy(x => new { X = (long)Math.Floor((double)(x.Longitude / cellSize)), Y = (long)Math.Floor((double)(x.Latitude / cellSize)) }).ToList();
        var clusters = groups.Where(x => x.Count() > 1).Select(group => new
        {
            id = $"z{(int)Math.Floor(zoom)}:{group.Key.X}:{group.Key.Y}",
            count = group.Count(),
            latitude = group.Average(x => x.Latitude),
            longitude = group.Average(x => x.Longitude),
            bounds = new { west = group.Min(x => x.Longitude), south = group.Min(x => x.Latitude), east = group.Max(x => x.Longitude), north = group.Max(x => x.Latitude) }
        }).ToList();
        var items = groups.Where(x => x.Count() == 1).Select(x => MapItem(x.Single())).ToList();
        return Results.Ok(new { clusters, items, total = rows.Count });
    }

    private static decimal CellSize(double zoom) => zoom switch
    {
        < 5 => 8m, < 7 => 3m, < 9 => .8m, < 11 => .2m, < 13 => .05m, < 15 => .012m, _ => .003m
    };

    private static object MapItem(MapRow row) => new
    {
        listingId = row.Id,
        row.Title,
        priceLabel = row.DealType == "Free" ? "Бесплатно" : row.Price is null ? "Цена не указана" : $"{row.Price:N0} ₽",
        latitude = row.Latitude,
        longitude = row.Longitude,
        previewImageUrl = row.Image ?? "/demo/images/placeholder.svg",
        locationLabel = row.City,
        category = row.Category,
        verifiedSeller = row.VerifiedSeller,
        detailUrl = $"/Listings/Details?id={row.Id}"
    };

    private static async Task ProxyAssetAsync(HttpContext context, IHttpClientFactory clients, string path)
    {
        var client = clients.CreateClient(ClientName);
        if (client.BaseAddress is null) { context.Response.StatusCode = 503; return; }
        using var request = new HttpRequestMessage(HttpMethod.Get, path + context.Request.QueryString);
        if (context.Request.Headers.Range.Count > 0 && RangeHeaderValue.TryParse(context.Request.Headers.Range, out var range)) request.Headers.Range = range;
        if (context.Request.Headers.IfNoneMatch.Count > 0)
            foreach (var value in context.Request.Headers.IfNoneMatch) if (EntityTagHeaderValue.TryParse(value, out var tag)) request.Headers.IfNoneMatch.Add(tag);
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            CopyHeader(response, context, "Accept-Ranges"); CopyHeader(response, context, "Content-Range"); CopyHeader(response, context, "ETag"); CopyHeader(response, context, "Last-Modified"); CopyHeader(response, context, "Cache-Control");
            if (response.Content.Headers.ContentType is not null) context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            if (response.Content.Headers.ContentLength is long length) context.Response.ContentLength = length;
            if (response.StatusCode is not HttpStatusCode.NotModified and not HttpStatusCode.NoContent)
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (HttpRequestException) { if (!context.Response.HasStarted) context.Response.StatusCode = 502; }
    }

    private static async Task ServeOrProxyAssetAsync(HttpContext context, string? path, IConfiguration configuration, IHttpClientFactory clients)
    {
        var localPath = ResolveLocalAssetPath(configuration["Maps:LocalAssetsRoot"], path);
        if (localPath is not null)
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            var result = Results.File(localPath, ContentTypeFor(localPath), enableRangeProcessing: true);
            await result.ExecuteAsync(context);
            return;
        }

        await ProxyAssetAsync(context, clients, $"/map-assets/{path ?? string.Empty}");
    }

    private static string? ResolveLocalAssetPath(string? configuredRoot, string? requestedPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot) || string.IsNullOrWhiteSpace(requestedPath)) return null;
        var root = Path.GetFullPath(configuredRoot) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(root, requestedPath.Replace('/', Path.DirectorySeparatorChar)));
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate) ? candidate : null;
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pmtiles" => "application/vnd.pmtiles",
        ".pbf" => "application/x-protobuf",
        ".json" => "application/json",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };

    private static void CopyHeader(HttpResponseMessage source, HttpContext target, string name)
    {
        if (source.Headers.TryGetValues(name, out var values) || source.Content.Headers.TryGetValues(name, out values)) target.Response.Headers[name] = values.ToArray();
    }

    private sealed record MapRow(Guid Id, string Title, decimal? Price, string DealType, decimal Latitude, decimal Longitude, string City, string Category, bool VerifiedSeller, string? Image);
}
