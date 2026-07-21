using Marketplace.Web.Modules.Geo;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Listings;

public static class DevelopmentListingSeed
{
    private static readonly Guid BmwListingId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private sealed record Seed(Guid Id, string CategorySlug, string Title, string Description, decimal Price, string Image, string District, decimal Latitude, decimal Longitude, int PublishedDaysAgo, bool Verified, string Condition = "Used", string DealType = "FixedPrice");

    private static readonly Seed[] Listings =
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "prodazha-kvartir", "2-комн. квартира, 55 м²", "Светлая квартира в новом доме. Удобная планировка, окна во двор.", 7_850_000, "/demo/images/apartment.png", "м. Солнцево", 55.645m, 37.394m, 0, true),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "sedany", "BMW 5 серия, 2018", "2.0 л / 184 л.с., автомат, полный привод. Отличное состояние.", 2_150_000, "/demo/images/bmw.png", "м. Крылатское", 55.756m, 37.408m, 1, true),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "divany", "Диван раскладной", "Современный диван в отличном состоянии. Механизм еврокнижка.", 32_000, "/demo/images/sofa.png", "м. Дмитровская", 55.808m, 37.581m, 0, false),
        new(Guid.Parse("10000000-0000-0000-0000-000000000004"), "smartfony", "iPhone 13, 128 ГБ", "Синий, в идеальном состоянии. Полный комплект, чек, гарантия.", 64_990, "/demo/images/iphone.png", "м. Тверская", 55.765m, 37.605m, 2, true, "New"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000005"), "stoly-i-stulya", "Кресло компьютерное", "Эргономичное кресло с поясничной поддержкой и регулировками.", 8_500, "/demo/images/chair.png", "м. Алексеевская", 55.807m, 37.638m, 0, false, "Used", "Exchange"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000006"), "bytovaya-tehnika", "Стиральная машина Bosch", "Загрузка 6 кг, 1000 об/мин. Работает отлично.", 16_000, "/demo/images/washer.png", "м. Бабушкинская", 55.870m, 37.664m, 1, true),
        new(Guid.Parse("10000000-0000-0000-0000-000000000007"), "stoly-i-stulya", "Стол обеденный", "Деревянный стол, 140×80 см. Состояние отличное.", 12_000, "/demo/images/table.png", "м. Павелецкая", 55.731m, 37.637m, 3, false, "Used", "Free"),
        new(Guid.Parse("10000000-0000-0000-0000-000000000008"), "odezhda-i-obuv", "Кроссовки Nike Air Force 1", "Оригинал, размер 42. Носились несколько раз.", 4_200, "/demo/images/shoes.png", "м. Молодёжная", 55.741m, 37.416m, 1, false, "New")
    ];

    public static Guid[] FeaturedIds => Listings.Select(x => x.Id).ToArray();

    public static async Task InitializeAsync(MarketplaceDbContext db, UserManager<ApplicationUser> userManager, CancellationToken cancellationToken = default)
    {
        var verifiedSeller = await EnsureSellerAsync(userManager, "demo.verified@marketplace.local", "Проверенный продавец", true);
        var regularSeller = await EnsureSellerAsync(userManager, "demo.seller@marketplace.local", "Частный продавец", false);
        var categories = await db.CatalogCategories.AsNoTracking().ToDictionaryAsync(x => x.Slug, cancellationToken);
        var schemaVersions = (await db.CategorySchemaVersions.AsNoTracking().Where(x => x.IsPublished).ToListAsync(cancellationToken)).GroupBy(x => x.CategoryId).ToDictionary(x => x.Key, x => x.OrderByDescending(v => v.Version).First());
        var seedIds = Listings.Select(seed => seed.Id).ToArray();
        var existingListings = await db.Listings.Where(x => seedIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        foreach (var seed in Listings)
        {
            if (existingListings.TryGetValue(seed.Id, out var existingListing))
            {
                existingListing.Condition = seed.Condition;
                existingListing.DealType = seed.DealType;
                continue;
            }
            if (!categories.TryGetValue(seed.CategorySlug, out var category)) continue;
            var publishedAt = now.AddDays(-seed.PublishedDaysAgo).AddMinutes(-seed.Id.ToByteArray()[15]);
            var listing = new Listing
            {
                Id = seed.Id,
                OwnerId = seed.Verified ? verifiedSeller.Id : regularSeller.Id,
                CategoryId = category.Id,
                CategorySchemaVersionId = schemaVersions.GetValueOrDefault(category.Id)?.Id,
                Title = seed.Title,
                Description = seed.Description,
                DealType = seed.DealType,
                Price = seed.Price,
                Condition = seed.Condition,
                Status = "Active",
                PublishedAt = publishedAt,
                CreatedAt = publishedAt,
                UpdatedAt = publishedAt
            };
            listing.Media.Add(new ListingMedia { Id = Guid.NewGuid(), ListingId = listing.Id, Url = seed.Image, Alt = seed.Title, IsPrimary = true });
            listing.Location = new ListingLocation { Id = Guid.NewGuid(), ListingId = listing.Id, City = "Москва", District = seed.District, PublicLatitude = seed.Latitude, PublicLongitude = seed.Longitude };
            listing.StatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = listing.Id, Status = "Active", Reason = "Development featured seed", CreatedAt = publishedAt });
            db.Listings.Add(listing);
        }
        await db.SaveChangesAsync(cancellationToken);

        var bmwGallery = new[]
        {
            new { Url = "/demo/images/bmw-rear.png", Alt = "BMW 5 серия — вид сзади", SortOrder = 1 },
            new { Url = "/demo/images/bmw-side.png", Alt = "BMW 5 серия — вид сбоку", SortOrder = 2 }
        };
        var existingBmwMedia = (await db.ListingMedia.AsNoTracking()
            .Where(x => x.ListingId == BmwListingId)
            .Select(x => x.Url)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var image in bmwGallery.Where(image => !existingBmwMedia.Contains(image.Url)))
        {
            db.ListingMedia.Add(new ListingMedia
            {
                Id = Guid.NewGuid(),
                ListingId = BmwListingId,
                Url = image.Url,
                Alt = image.Alt,
                SortOrder = image.SortOrder,
                IsPrimary = false
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ApplicationUser> EnsureSellerAsync(UserManager<ApplicationUser> userManager, string email, string displayName, bool verified)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null) return user;
        user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = verified, DisplayName = displayName, City = "Москва" };
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded) throw new InvalidOperationException($"Unable to create development seller: {string.Join("; ", created.Errors.Select(x => x.Code))}");
        var role = await userManager.AddToRoleAsync(user, "Member");
        if (!role.Succeeded) throw new InvalidOperationException($"Unable to assign development seller role: {string.Join("; ", role.Errors.Select(x => x.Code))}");
        return user;
    }
}
