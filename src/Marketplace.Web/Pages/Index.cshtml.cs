using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Marketplace.Web.Modules.Geo;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.WebUtilities;
using Marketplace.Web.Modules.Recommendations;
using System.Globalization;

namespace Marketplace.Web.Pages;

public class IndexModel(IListingCatalog listingCatalog, MarketplaceDbContext db, IIpGeoAdapter ipGeo, IMapProviderAdapter mapProvider) : PageModel
{
    public sealed record CategoryOption(string Slug, string Name);
    public sealed record FilterField(string Code, string Name, string? Unit, IReadOnlyList<string> Options, string? Value);
    public IReadOnlyList<ListingCard> Listings { get; private set; } = [];
    public int Total { get; private set; }
    public bool FromDatabase { get; private set; }
    public string? Query { get; private set; }
    public string Sort { get; private set; } = "recommended";
    public string? Category { get; private set; }
    public decimal? MinPrice { get; private set; }
    public decimal? MaxPrice { get; private set; }
    public string? Condition { get; private set; }
    public string? DealType { get; private set; }
    public bool HasPhoto { get; private set; }
    public IReadOnlyList<CategoryOption> Categories { get; private set; } = [];
    public IReadOnlyList<CategoryOption> HeaderCategories { get; private set; } = [];
    public IReadOnlyList<CategoryOption> Subcategories { get; private set; } = [];
    public string? SelectedCategoryName { get; private set; }
    public string? SelectedRootCategorySlug { get; private set; }
    public string? SelectedSubcategorySlug { get; private set; }
    public IReadOnlyList<FilterField> CategoryFilters { get; private set; } = [];
    public int ActiveFilterCount { get; private set; }
    public IReadOnlyCollection<string> Cities => [.. GeoPrivacy.SupportedCities, "Россия"];
    public string SelectedCity { get; private set; } = "Москва";
    public string SelectedAddress { get; private set; } = "Москва";
    public decimal? SelectedLatitude { get; private set; }
    public decimal? SelectedLongitude { get; private set; }
    public string GeoSource { get; private set; } = "fallback";
    public int Radius { get; private set; } = 30;
    public string ViewMode { get; private set; } = "split";
    public int Zoom { get; private set; } = 11;
    public IReadOnlyList<MapFeature> MapFeatures { get; private set; } = [];
    public string MapProviderName => mapProvider.ProviderName;
    public bool CanSaveSearch { get; private set; }
    public bool IsSearchSaved { get; private set; }
    public string CurrentSearchQuery { get; private set; } = string.Empty;
    public string CurrentSearchLabel { get; private set; } = string.Empty;
    public IReadOnlySet<Guid> FavoriteListingIds { get; private set; } = new HashSet<Guid>();
    public IReadOnlyDictionary<Guid, IReadOnlyList<string>> ListingImages { get; private set; } = new Dictionary<Guid, IReadOnlyList<string>>();
    public int FavoriteCount { get; private set; }
    public int UnreadMessageCount { get; private set; }

    public async Task OnGetAsync(string? q, string? category, decimal? minPrice, decimal? maxPrice, string? condition, string? dealType, bool hasPhoto, string? sort, string? city, string? address, decimal? latitude, decimal? longitude, int radius = 30, string? mode = null, int zoom = 11, CancellationToken cancellationToken = default)
    {
        var guess = await ipGeo.ResolveAsync(HttpContext.Connection.RemoteIpAddress, cancellationToken);
        SelectedCity = string.IsNullOrWhiteSpace(city) ? Request.Cookies["marketplace.city"] ?? guess.City : city;
        if (!Cities.Contains(SelectedCity)) SelectedCity = "Москва";
        var hasManualAddress = !string.IsNullOrWhiteSpace(address) && IsRussianPoint(latitude, longitude);
        SelectedAddress = hasManualAddress ? address!.Trim() : Request.Cookies["marketplace.address"] ?? SelectedCity;
        SelectedLatitude = hasManualAddress ? latitude : ParseCoordinateCookie("marketplace.address-latitude");
        SelectedLongitude = hasManualAddress ? longitude : ParseCoordinateCookie("marketplace.address-longitude");
        if (!IsRussianPoint(SelectedLatitude, SelectedLongitude)) { SelectedLatitude = null; SelectedLongitude = null; SelectedAddress = SelectedCity; }
        GeoSource = hasManualAddress || !string.IsNullOrWhiteSpace(city) ? "manual" : Request.Cookies.ContainsKey("marketplace.address") || Request.Cookies.ContainsKey("marketplace.city") ? "saved" : guess.Source;
        Radius = Math.Clamp(radius, 1, 500);
        ViewMode = mode is "list" or "map" or "split" ? mode : Request.Cookies["marketplace.map-mode"] ?? "split";
        Zoom = Math.Clamp(zoom, 4, 18);
        if (!string.IsNullOrWhiteSpace(city)) Response.Cookies.Append("marketplace.city", SelectedCity, new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
        if (hasManualAddress)
        {
            var cookieOptions = new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax };
            Response.Cookies.Append("marketplace.address", SelectedAddress, cookieOptions);
            Response.Cookies.Append("marketplace.address-latitude", SelectedLatitude!.Value.ToString(CultureInfo.InvariantCulture), cookieOptions);
            Response.Cookies.Append("marketplace.address-longitude", SelectedLongitude!.Value.ToString(CultureInfo.InvariantCulture), cookieOptions);
        }
        if (!string.IsNullOrWhiteSpace(mode)) Response.Cookies.Append("marketplace.map-mode", ViewMode, new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
        Query = q?.Trim(); Category = category; MinPrice = minPrice; MaxPrice = maxPrice; Condition = condition; DealType = dealType; HasPhoto = hasPhoto;
        Sort = string.IsNullOrWhiteSpace(sort) ? Request.Cookies["marketplace.search-sort"] ?? "recommended" : sort;
        if (!string.IsNullOrWhiteSpace(sort)) Response.Cookies.Append("marketplace.search-sort", Sort, new CookieOptions { MaxAge = TimeSpan.FromDays(365), SameSite = SameSiteMode.Lax });
        var attributes = Request.Query.Where(x => x.Key.StartsWith("attr_", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Key[5..], x => x.Value.ToString());
        var activeCategories = await db.CatalogCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.ParentId, x.Slug, x.Name })
            .ToListAsync(cancellationToken);
        var headerCategoryRows = activeCategories.Where(x => x.ParentId == null).Take(15).ToList();
        HeaderCategories = headerCategoryRows.Select(x => new CategoryOption(x.Slug, x.Name)).ToList();
        var publicCategoryIds = headerCategoryRows.Select(x => x.Id).ToHashSet();
        while (true)
        {
            var childIds = activeCategories
                .Where(x => x.ParentId is Guid parentId && publicCategoryIds.Contains(parentId) && !publicCategoryIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToList();
            if (childIds.Count == 0) break;
            publicCategoryIds.UnionWith(childIds);
        }
        Categories = activeCategories.Where(x => publicCategoryIds.Contains(x.Id)).OrderBy(x => x.Name).Select(x => new CategoryOption(x.Slug, x.Name)).ToList();
        var selectedCategory = activeCategories.FirstOrDefault(x => x.Slug == Category);
        SelectedCategoryName = selectedCategory?.Name;
        if (selectedCategory is not null)
        {
            var categoryById = activeCategories.ToDictionary(x => x.Id);
            var rootCategory = selectedCategory;
            string? directSubcategorySlug = null;
            while (rootCategory.ParentId is Guid parentId && categoryById.TryGetValue(parentId, out var parentCategory))
            {
                directSubcategorySlug = rootCategory.Slug;
                rootCategory = parentCategory;
            }

            SelectedRootCategorySlug = rootCategory.Slug;
            SelectedSubcategorySlug = selectedCategory.Id == rootCategory.Id ? null : directSubcategorySlug;
            Subcategories = activeCategories
                .Where(x => x.ParentId == rootCategory.Id)
                .Select(x => new CategoryOption(x.Slug, x.Name))
                .ToList();
        }
        if (!string.IsNullOrWhiteSpace(Category))
        {
            CategoryFilters = await db.CategorySchemaVersions.AsNoTracking().Where(x => x.Category.Slug == Category && x.IsPublished).OrderByDescending(x => x.Version).SelectMany(x => x.Attributes).OrderBy(x => x.SortOrder).Select(x => new FilterField(x.Code, x.Name, x.Unit, string.IsNullOrWhiteSpace(x.OptionsJson) ? Array.Empty<string>() : JsonSerializer.Deserialize<string[]>(x.OptionsJson)!, attributes.GetValueOrDefault(x.Code))).ToListAsync(cancellationToken);
        }
        ActiveFilterCount = (string.IsNullOrWhiteSpace(Category) ? 0 : 1)
            + (MinPrice.HasValue || MaxPrice.HasValue ? 1 : 0)
            + (string.IsNullOrWhiteSpace(Condition) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(DealType) ? 0 : 1)
            + (HasPhoto ? 1 : 0)
            + attributes.Count;
        var result = await listingCatalog.SearchAsync(new CatalogSearchRequest(Query, Category, MinPrice, MaxPrice, Condition, DealType, HasPhoto, Sort, attributes, SelectedCity, Radius, CenterLatitude: SelectedLatitude, CenterLongitude: SelectedLongitude), cancellationToken);
        Listings = result.Items; Total = result.Total; FromDatabase = result.FromDatabase;
        MapFeatures = mapProvider.BuildFeatures(Listings, Zoom);
        var visibleListingIds = Listings.Select(x => Guid.TryParse(x.Id, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToList();
        var mediaRows = await db.ListingMedia.AsNoTracking()
            .Where(x => visibleListingIds.Contains(x.ListingId))
            .Select(x => new { x.ListingId, x.Url, x.IsPrimary, x.SortOrder })
            .ToListAsync(cancellationToken);
        ListingImages = mediaRows
            .GroupBy(x => x.ListingId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.SortOrder)
                    .Select(x => x.Url)
                    .ToList());

        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
        {
            FavoriteListingIds = (await db.Favorites.AsNoTracking().Where(x => x.UserId == currentUserId && visibleListingIds.Contains(x.ListingId)).Select(x => x.ListingId).ToListAsync(cancellationToken)).ToHashSet();
            FavoriteCount = await db.Favorites.CountAsync(x => x.UserId == currentUserId, cancellationToken);
            UnreadMessageCount = await db.ChatMessages.CountAsync(x => x.SenderId != currentUserId && x.ReadAt == null && (x.Conversation.SellerId == currentUserId || x.Conversation.BuyerId == currentUserId), cancellationToken);
        }

        var searchRequest = new CatalogSearchRequest(Query, Category, MinPrice, MaxPrice, Condition, DealType, HasPhoto, Sort, attributes, SelectedCity, Radius, CenterLatitude: SelectedLatitude, CenterLongitude: SelectedLongitude);
        CanSaveSearch = searchRequest.HasCriteria;
        if (CanSaveSearch)
        {
            var values = Request.Query.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            values["city"] = SelectedCity;
            values["radius"] = Radius.ToString();
            CurrentSearchQuery = RecommendationQuery.Normalize(values);
            CurrentSearchLabel = RecommendationQuery.Label(values);
            if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                var fingerprint = RecommendationQuery.Fingerprint(CurrentSearchQuery);
                IsSearchSaved = await db.SavedSearches.AnyAsync(x => x.UserId == userId && x.Fingerprint == fingerprint, cancellationToken);
                var user = await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId, cancellationToken);
                if (user.UseHistoryForRecommendations)
                {
                    var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
                    await db.SearchHistoryEntries.Where(x => x.UserId == userId && x.SearchedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
                    var history = await db.SearchHistoryEntries.FirstOrDefaultAsync(x => x.UserId == userId && x.Fingerprint == fingerprint, cancellationToken);
                    if (history is null) db.SearchHistoryEntries.Add(new SearchHistoryEntry { Id = Guid.NewGuid(), UserId = userId, Fingerprint = fingerprint, Label = CurrentSearchLabel, QueryString = CurrentSearchQuery });
                    else { history.Label = CurrentSearchLabel; history.QueryString = CurrentSearchQuery; history.SearchedAt = DateTimeOffset.UtcNow; }
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }

    public async Task<IActionResult> OnPostSaveSearchAsync(string searchQuery, int total, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        if (string.IsNullOrWhiteSpace(searchQuery) || searchQuery.Length > 2000) return RedirectToPage();
        var parsed = QueryHelpers.ParseQuery(searchQuery);
        var normalized = RecommendationQuery.Normalize(parsed);
        var fingerprint = RecommendationQuery.Fingerprint(normalized);
        if (!await db.SavedSearches.AnyAsync(x => x.UserId == userId && x.Fingerprint == fingerprint, cancellationToken))
        {
            db.SavedSearches.Add(new SavedSearch { Id = Guid.NewGuid(), UserId = userId, Fingerprint = fingerprint, Name = RecommendationQuery.Label(parsed), QueryString = normalized, LastKnownCount = Math.Max(0, total) });
            await db.SaveChangesAsync(cancellationToken);
            TempData["Notice"] = "Поиск сохранён. Сообщим, когда появятся новые объявления.";
        }
        return LocalRedirect("/" + normalized);
    }

    public async Task<IActionResult> OnGetMoreAsync(int offset, string? q, string? category, decimal? minPrice, decimal? maxPrice, string? condition, string? dealType, bool hasPhoto, string? sort, string? city, decimal? latitude, decimal? longitude, int radius = 30, CancellationToken cancellationToken = default)
    {
        var selectedCity = string.IsNullOrWhiteSpace(city) ? Request.Cookies["marketplace.city"] ?? "Москва" : city;
        var selectedSort = string.IsNullOrWhiteSpace(sort) ? Request.Cookies["marketplace.search-sort"] ?? "recommended" : sort;
        var attributes = Request.Query
            .Where(x => x.Key.StartsWith("attr_", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key[5..], x => x.Value.ToString());
        var result = await listingCatalog.SearchAsync(new CatalogSearchRequest(q?.Trim(), category, minPrice, maxPrice, condition, dealType, hasPhoto, selectedSort, attributes, selectedCity, Math.Clamp(radius, 1, 500), Math.Max(0, offset), 12, latitude, longitude), cancellationToken);
        var listingIds = result.Items.Select(x => Guid.TryParse(x.Id, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToList();
        var media = await db.ListingMedia.AsNoTracking().Where(x => listingIds.Contains(x.ListingId))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SortOrder)
            .Select(x => new { x.ListingId, x.Url }).ToListAsync(cancellationToken);
        var mediaByListing = media.GroupBy(x => x.ListingId).ToDictionary(x => x.Key, x => x.Select(m => m.Url).ToList());
        var favoriteIds = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? (await db.Favorites.AsNoTracking().Where(x => x.UserId == userId && listingIds.Contains(x.ListingId)).Select(x => x.ListingId).ToListAsync(cancellationToken)).ToHashSet()
            : [];
        var returnValues = Request.Query.Where(x => x.Key is not "handler" and not "offset").ToDictionary(x => x.Key, x => (string?)x.Value.ToString());
        var returnUrl = $"/{QueryString.Create(returnValues).Value}";

        return new JsonResult(new
        {
            items = result.Items.Select(item =>
            {
                var hasId = Guid.TryParse(item.Id, out var id);
                var images = hasId && mediaByListing.TryGetValue(id, out var stored) && stored.Count > 0 ? stored : [item.Image];
                return new
                {
                    item.Id, item.Title, item.PriceLabel, item.Description, item.Location, item.PublishedLabel, item.VerifiedIdentity, item.Negotiable,
                    images,
                    isFavorite = hasId && favoriteIds.Contains(id),
                    detailUrl = hasId ? Url.Page("/Listings/Details", new { id = item.Id }) : Url.Page("/Catalog"),
                    favoriteUrl = User.Identity?.IsAuthenticated == true ? "/?handler=ToggleFavorite" : Url.Page("/Account/SignIn", new { ReturnUrl = Url.Page("/Listings/Details", new { id, intent = "favorite" }) }),
                    returnUrl
                };
            }),
            total = result.Total,
            hasMore = offset + result.Items.Count < result.Total
        });
    }

    public async Task<IActionResult> OnPostToggleFavoriteAsync(Guid id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Challenge();
        var listing = await db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == "Active", cancellationToken);
        if (listing is null) return NotFound();
        var stored = await db.Favorites.FirstOrDefaultAsync(x => x.UserId == userId && x.ListingId == id, cancellationToken);
        if (stored is null) db.Favorites.Add(new Modules.Engagement.Favorite { Id = Guid.NewGuid(), UserId = userId, ListingId = id, PriceWhenAdded = listing.Price });
        else db.Favorites.Remove(stored);
        await db.SaveChangesAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage();
    }

    public IActionResult OnGetResolveLocation(double latitude, double longitude)
    {
        var city = GeoPrivacy.ResolveNearestCity(latitude, longitude);
        return new JsonResult(new { city, address = city, latitude, longitude, source = "browser" });
    }

    private decimal? ParseCoordinateCookie(string name) => decimal.TryParse(Request.Cookies[name], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static bool IsRussianPoint(decimal? latitude, decimal? longitude) => latitude is >= 41m and <= 82m && longitude is >= 19m and <= 180m;

    public string ModeUrl(string mode)
    {
        var values = Request.Query.ToDictionary(x => x.Key, x => (string?)x.Value.ToString());
        values["mode"] = mode;
        return $"/{QueryString.Create(values).Value}";
    }

    public string ZoomUrl(int zoom)
    {
        var values = Request.Query.ToDictionary(x => x.Key, x => (string?)x.Value.ToString());
        values["zoom"] = Math.Clamp(zoom, 4, 18).ToString();
        return $"/{QueryString.Create(values).Value}";
    }

    public string QueryUrl(string key, string? value)
    {
        var values = Request.Query.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) values.Remove(key);
        else values[key] = value;
        return $"/{QueryString.Create(values).Value}";
    }

    public static string MarkerPositionClasses(double x, double y)
    {
        static (int Major, int Unit, int Tenth) Parts(double value)
        {
            var tenths = Math.Clamp((int)Math.Round(value * 10, MidpointRounding.AwayFromZero), 0, 1000);
            return (tenths / 100, tenths / 10 % 10, tenths % 10);
        }

        var xParts = Parts(x);
        var yParts = Parts(y);
        return $"map-x-major-{xParts.Major} map-x-unit-{xParts.Unit} map-x-tenth-{xParts.Tenth} " +
               $"map-y-major-{yParts.Major} map-y-unit-{yParts.Unit} map-y-tenth-{yParts.Tenth}";
    }

}
