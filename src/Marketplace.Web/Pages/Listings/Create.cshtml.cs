using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Listings;
using Marketplace.Web.Modules.Moderation;
using Marketplace.Web.Modules.Geo;
using Marketplace.Web.Modules.Engagement;
using Marketplace.Web.Modules.Monetization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Marketplace.Web.Modules.Storage;

namespace Marketplace.Web.Pages.Listings;

[Authorize]
public sealed class CreateModel(UserManager<ApplicationUser> userManager, MarketplaceDbContext db, IWebHostEnvironment environment, QuotaService quotaService) : PageModel
{
    public sealed class ListingInput { public Guid? Id { get; set; } [Required(ErrorMessage = "Укажите название объявления."), StringLength(160, ErrorMessage = "Название не должно превышать 160 символов.")] public string Title { get; set; } = string.Empty; [Required(ErrorMessage = "Выберите категорию.")] public Guid? CategoryId { get; set; } public string DealType { get; set; } = "FixedPrice"; [Range(0, 999999999999, ErrorMessage = "Цена не может быть отрицательной.")] public decimal? Price { get; set; } public string Condition { get; set; } = "Used"; [StringLength(5000, ErrorMessage = "Описание не должно превышать 5000 символов.")] public string? Description { get; set; } [StringLength(500, ErrorMessage = "Адрес слишком длинный.")] public string? AddressText { get; set; } [Range(41, 82, ErrorMessage = "Некорректная широта.")] public decimal? Latitude { get; set; } [Range(19, 191, ErrorMessage = "Некорректная долгота.")] public decimal? Longitude { get; set; } public bool ShowExactAddress { get; set; } [Range(typeof(bool), "true", "true", ErrorMessage = "Подтвердите согласие с правилами.")] public bool AcceptRules { get; set; } }
    public sealed record Field(string Code, string Name, string? Unit, string? Value);
    public sealed record MediaView(string Url, string Alt);
    [BindProperty] public ListingInput Input { get; set; } = new();
    [BindProperty] public int Step { get; set; } = 1;
    [BindProperty] public List<IFormFile> Photos { get; set; } = [];
    [TempData] public string? StatusMessage { get; set; }
    public IReadOnlyList<CatalogCategory> Categories { get; private set; } = [];
    public IReadOnlyList<Field> Attributes { get; private set; } = [];
    public IReadOnlyList<MediaView> Media { get; private set; } = [];
    public string CategoryName { get; private set; } = "—";
    public string PriceLabel => Input.DealType == "Free" ? "Бесплатно" : Input.Price is null ? "Цена не указана" : $"{Input.Price:N0} ₽";

    public async Task<IActionResult> OnGetAsync(Guid? id, int step = 1) { await LoadCategoriesAsync(); var user = await userManager.GetUserAsync(User); if (id is null) { ApplyProfileAddress(user); return Page(); } var listing = await OwnedAsync(id.Value); if (listing is null) return NotFound(); Map(listing); var location = await db.ListingLocations.AsNoTracking().FirstOrDefaultAsync(x => x.ListingId == listing.Id); if (location is not null && !string.IsNullOrWhiteSpace(location.ExactAddress)) { Input.AddressText = location.ExactAddress; Input.Latitude = location.ExactLatitude; Input.Longitude = location.ExactLongitude; Input.ShowExactAddress = location.IsExactPointPublic; } else ApplyProfileAddress(user); Step = Math.Clamp(step, 1, 3); await LoadAttributesAsync(listing.Id, listing.CategoryId); Media = await db.ListingMedia.AsNoTracking().Where(x => x.ListingId == listing.Id).OrderBy(x => x.SortOrder).Select(x => new MediaView(x.Url, x.Alt)).ToListAsync(); return Page(); }
    public async Task<IActionResult> OnPostSaveBasicAsync()
    {
        var user = await userManager.GetUserAsync(User); if (user is null) return Challenge(); ModelState.Remove("Input.Description"); ModelState.Remove("Input.AcceptRules");
        if (!ModelState.IsValid) { await LoadCategoriesAsync(); return Page(); }
        var isExisting = Input.Id is not null; var listing = Input.Id is null ? new Listing { Id = Guid.NewGuid(), OwnerId = user.Id } : await OwnedAsync(Input.Id.Value); if (listing is null) return NotFound();
        var category = await db.CatalogCategories.FindAsync(Input.CategoryId); if (category is null) { ModelState.AddModelError("Input.CategoryId", "Категория не найдена."); await LoadCategoriesAsync(); return Page(); }
        if (listing.CategoryId != Guid.Empty && listing.CategoryId != category.Id)
        {
            listing.Status = "Archived"; db.ListingStatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Status = "Archived", Reason = "Категория изменена — создано новое объявление." }); db.Listings.Add(new Listing { Id = Guid.NewGuid(), OwnerId = user.Id, CategoryId = category.Id, Title = Input.Title, DealType = Input.DealType, Price = Input.Price, Condition = Input.Condition, RecreatedFromListingId = listing.Id });
        }
        else
        {
            var oldPrice = listing.Price; var priceChanged = isExisting && (oldPrice != Input.Price || listing.DealType != Input.DealType); var contentChanged = isExisting && (listing.Title != Input.Title.Trim() || listing.Condition != Input.Condition);
            listing.CategoryId = category.Id; listing.Title = Input.Title.Trim(); listing.DealType = Input.DealType; listing.Price = Input.Price; listing.Condition = Input.Condition; listing.UpdatedAt = DateTimeOffset.UtcNow; listing.CategorySchemaVersionId = await db.CategorySchemaVersions.Where(x => x.CategoryId == category.Id && x.IsPublished).OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
            if (Input.Id is null) db.Listings.Add(listing);
            else
            {
                if (priceChanged) db.ListingPriceHistory.Add(new ListingPriceHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Price = listing.Price, DealType = listing.DealType });
                if (oldPrice is not null && Input.Price is not null && Input.Price < oldPrice)
                {
                    var favorites = await db.Favorites.Where(x => x.ListingId == listing.Id).ToListAsync();
                    foreach (var favorite in favorites) { db.UserNotifications.Add(new UserNotification { Id = Guid.NewGuid(), UserId = favorite.UserId, Type = "PriceDrop", Text = $"Цена снижена: {listing.Title} — {Input.Price:N0} ₽", Link = $"/Listings/Details?id={listing.Id}" }); favorite.PriceWhenAdded = Input.Price; }
                }
                if (contentChanged) db.ListingRevisions.Add(new ListingRevision { Id = Guid.NewGuid(), ListingId = listing.Id, RevisionNumber = await db.ListingRevisions.CountAsync(x => x.ListingId == listing.Id) + 1, SnapshotJson = JsonSerializer.Serialize(new { listing.Title, listing.Price, listing.DealType, listing.Condition }) });
            }
        }
        await db.SaveChangesAsync(); var target = Input.Id is null ? listing.Id : listing.CategoryId == category.Id ? listing.Id : (await db.Listings.OrderByDescending(x => x.CreatedAt).FirstAsync(x => x.OwnerId == user.Id)).Id; return RedirectToPage(new { id = target, step = 2 });
    }
    public async Task<IActionResult> OnPostSaveDetailsAsync()
    {
        ModelState.Remove("Input.Title"); ModelState.Remove("Input.CategoryId"); ModelState.Remove("Input.AcceptRules");
        var listing = Input.Id is null ? null : await OwnedAsync(Input.Id.Value);
        if (listing is null) return NotFound();
        var oldDescription = listing.Description;
        listing.Description = Input.Description?.Trim();
        listing.UpdatedAt = DateTimeOffset.UtcNow;
        if (Input.Latitude is not null ^ Input.Longitude is not null) ModelState.AddModelError("Input.AddressText", "Выберите адрес из подсказок.");
        if (!ModelState.IsValid) { Step = 2; await LoadAttributesAsync(listing.Id, listing.CategoryId); Media = await db.ListingMedia.AsNoTracking().Where(x => x.ListingId == listing.Id).OrderBy(x => x.SortOrder).Select(x => new MediaView(x.Url, x.Alt)).ToListAsync(); return Page(); }
        var mediaCount = await db.ListingMedia.CountAsync(x => x.ListingId == listing.Id);
        var storedFiles = new List<StoredUpload>();
        await using var transaction = await db.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        try
        {
            foreach (var photo in Photos.Take(Math.Max(0, 10 - mediaCount)))
            {
                var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                var allowed = (extension is ".jpg" or ".jpeg" or ".png" or ".webp") && (photo.ContentType is "image/jpeg" or "image/png" or "image/webp") && photo.Length <= 10 * 1024 * 1024;
                if (!allowed || photo.Length == 0 || !await UploadContentValidator.MatchesDeclaredTypeAsync(photo, HttpContext.RequestAborted)) { ModelState.AddModelError(nameof(Photos), $"Файл «{Path.GetFileName(photo.FileName)}» не загружен: разрешены JPEG, PNG и WebP до 10 МБ."); continue; }
                var directory = Path.Combine(environment.WebRootPath, "uploads", listing.Id.ToString());
                var stored = await AtomicUploadStorage.SaveAsync(photo, directory, extension, HttpContext.RequestAborted);
                storedFiles.Add(stored);
                db.ListingMedia.Add(new ListingMedia { Id = Guid.NewGuid(), ListingId = listing.Id, Url = $"/uploads/{listing.Id}/{stored.FileName}?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", Alt = listing.Title, IsPrimary = mediaCount == 0, SortOrder = mediaCount++ });
            }
            if (Photos.Count > 10 - (mediaCount - storedFiles.Count)) ModelState.AddModelError(nameof(Photos), "В объявлении может быть не более 10 фотографий.");
            if (!ModelState.IsValid)
            {
                await transaction.RollbackAsync(HttpContext.RequestAborted); AtomicUploadStorage.Cleanup(storedFiles); Step = 2;
                await LoadAttributesAsync(listing.Id, listing.CategoryId); Media = await db.ListingMedia.AsNoTracking().Where(x => x.ListingId == listing.Id).OrderBy(x => x.SortOrder).Select(x => new MediaView(x.Url, x.Alt)).ToListAsync(); return Page();
            }
            foreach (var key in Request.Form.Keys.Where(x => x.StartsWith("Attribute_", StringComparison.Ordinal)))
            {
                var code = key[10..]; var value = Request.Form[key].ToString().Trim();
                var valueEntity = await db.ListingAttributeValues.FirstOrDefaultAsync(x => x.ListingId == listing.Id && x.AttributeCode == code);
                if (valueEntity is null) db.ListingAttributeValues.Add(new ListingAttributeValue { Id = Guid.NewGuid(), ListingId = listing.Id, AttributeCode = code, ValueJson = JsonSerializer.Serialize(value) });
                else valueEntity.ValueJson = JsonSerializer.Serialize(value);
            }
            if (Input.Latitude is decimal latitude && Input.Longitude is decimal longitude)
            {
                var location = await db.ListingLocations.FirstOrDefaultAsync(x => x.ListingId == listing.Id);
                var publicPoint = Input.ShowExactAddress ? (latitude, longitude) : GeoPrivacy.CreatePublicPointFromExact(listing.Id, latitude, longitude);
                if (location is null) { location = new ListingLocation { Id = Guid.NewGuid(), ListingId = listing.Id }; db.ListingLocations.Add(location); }
                location.City = GeoPrivacy.ResolveNearestCity((double)latitude, (double)longitude);
                location.ExactAddress = Input.AddressText?.Trim(); location.ExactLatitude = latitude; location.ExactLongitude = longitude;
                location.PublicLatitude = publicPoint.Item1; location.PublicLongitude = publicPoint.Item2; location.IsExactPointPublic = Input.ShowExactAddress; location.UpdatedAt = DateTimeOffset.UtcNow;
            }
            if (oldDescription != listing.Description && listing.Status != "Draft") db.ListingRevisions.Add(new ListingRevision { Id = Guid.NewGuid(), ListingId = listing.Id, RevisionNumber = await db.ListingRevisions.CountAsync(x => x.ListingId == listing.Id) + 1, SnapshotJson = JsonSerializer.Serialize(new { listing.Title, listing.Description, listing.Price }) });
            await db.SaveChangesAsync(HttpContext.RequestAborted);
            await transaction.CommitAsync(HttpContext.RequestAborted);
        }
        catch
        {
            AtomicUploadStorage.Cleanup(storedFiles);
            throw;
        }
        return RedirectToPage(new { id = listing.Id, step = 3 });
    }
    public async Task<IActionResult> OnPostPublishAsync()
    {
        var listing = Input.Id is null ? null : await OwnedAsync(Input.Id.Value);
        if (listing is null) return NotFound();
        if (!Input.AcceptRules) { Map(listing); Step = 3; ModelState.AddModelError("Input.AcceptRules", "Подтвердите согласие с правилами."); return Page(); }
        var active = await db.Listings.CountAsync(x => x.OwnerId == listing.OwnerId && new[] { "Active", "PendingManualReview", "NeedsCorrection", "PausedByOwner", "Quarantine" }.Contains(x.Status));
        var activeLimit = await quotaService.ActiveListingLimitAsync(listing.OwnerId);
        if (active >= activeLimit) { Map(listing); Step = 3; ModelState.AddModelError(string.Empty, $"Достигнут лимит {activeLimit} активных объявлений."); return Page(); }
        listing.PublishedAt = DateTimeOffset.UtcNow; listing.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await db.ListingLocations.AnyAsync(x => x.ListingId == listing.Id)) { var user = await userManager.GetUserAsync(User); var city = user?.City ?? "Москва"; var point = user?.CityLatitude is decimal cityLatitude && user.CityLongitude is decimal cityLongitude ? GeoPrivacy.CreateStablePublicPoint(listing.Id, cityLatitude, cityLongitude) : GeoPrivacy.CreateStablePublicPoint(listing.Id, city); db.ListingLocations.Add(new ListingLocation { Id = Guid.NewGuid(), ListingId = listing.Id, City = city, PublicLatitude = point.Latitude, PublicLongitude = point.Longitude }); }
        var moderationCase = DemoModerationEngine.Evaluate(listing); db.ModerationCases.Add(moderationCase); db.ListingPriceHistory.Add(new ListingPriceHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Price = listing.Price, DealType = listing.DealType }); db.ListingStatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Status = listing.Status }); db.ListingRevisions.Add(new ListingRevision { Id = Guid.NewGuid(), ListingId = listing.Id, RevisionNumber = await db.ListingRevisions.CountAsync(x => x.ListingId == listing.Id) + 1, SnapshotJson = JsonSerializer.Serialize(new { listing.Title, listing.Description, listing.Price, listing.DealType }) });
        await db.SaveChangesAsync(); StatusMessage = moderationCase.RiskLevel == "Critical" ? "Объявление помещено в карантин и ожидает проверки." : "Объявление отправлено на автоматическую проверку."; return RedirectToPage("/Listings/My");
    }
    private async Task<Listing?> OwnedAsync(Guid id) { var user = await userManager.GetUserAsync(User); return user is null ? null : await db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == user.Id); }
    private async Task LoadCategoriesAsync() => Categories = await db.CatalogCategories.AsNoTracking().Where(x => x.IsActive && db.CategorySchemaVersions.Any(s => s.CategoryId == x.Id && s.IsPublished)).OrderBy(x => x.Name).ToListAsync();
    private async Task LoadAttributesAsync(Guid listingId, Guid categoryId) { var schemaId = await db.CategorySchemaVersions.AsNoTracking().Where(x => x.CategoryId == categoryId && x.IsPublished).OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(); var definitions = schemaId is null ? [] : await db.CategorySchemaVersions.AsNoTracking().Where(x => x.Id == schemaId).SelectMany(x => x.Attributes).OrderBy(x => x.SortOrder).Select(x => new { x.Code, x.Name, x.Unit }).ToListAsync(); var values = await db.ListingAttributeValues.AsNoTracking().Where(x => x.ListingId == listingId).ToDictionaryAsync(x => x.AttributeCode, x => x.ValueJson); Attributes = definitions.Select(x => new Field(x.Code, x.Name, x.Unit, values.TryGetValue(x.Code, out var json) ? JsonSerializer.Deserialize<string>(json) : null)).ToList(); }
    private void Map(Listing x) { Input = new ListingInput { Id = x.Id, Title = x.Title, CategoryId = x.CategoryId, DealType = x.DealType, Price = x.Price, Condition = x.Condition, Description = x.Description }; CategoryName = db.CatalogCategories.Find(x.CategoryId)?.Name ?? "—"; }
    private void ApplyProfileAddress(ApplicationUser? user) { if (user is null || string.IsNullOrWhiteSpace(user.Address)) return; Input.AddressText = user.Address; Input.Latitude = user.CityLatitude; Input.Longitude = user.CityLongitude; }
}
