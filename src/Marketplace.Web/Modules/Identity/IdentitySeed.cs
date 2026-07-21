using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Identity;

public static class IdentitySeed
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "Member", "Moderator", "SeniorModerator", "Support", "CatalogAdministrator", "FinanceOperator", "FinanceController", "SecuritySpecialist", "Auditor", "SeniorAdministrator", "Owner", "Administrator" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
        if (!await db.FeatureFlags.AnyAsync())
        {
            db.FeatureFlags.AddRange(
                new Marketplace.Web.Modules.Administration.FeatureFlag { Id = Guid.NewGuid(), Key = "recommendations.enabled", Description = "Показывать rule-based рекомендации.", IsEnabled = true },
                new Marketplace.Web.Modules.Administration.FeatureFlag { Id = Guid.NewGuid(), Key = "saved-search.notifications", Description = "Создавать уведомления по сохранённым поискам.", IsEnabled = true },
                new Marketplace.Web.Modules.Administration.FeatureFlag { Id = Guid.NewGuid(), Key = "chat.attachments", Description = "Разрешать безопасные вложения в чате.", IsEnabled = true });
            await db.SaveChangesAsync();
        }
        if (!await db.PlanDefinitions.AnyAsync())
        {
            db.PlanDefinitions.AddRange(
                new Marketplace.Web.Modules.Monetization.PlanDefinition { Id = Guid.NewGuid(), Code = "free", Name = "Бесплатный", MonthlyPrice = 0, AnnualPrice = 0, ActiveListingLimit = 10 },
                new Marketplace.Web.Modules.Monetization.PlanDefinition { Id = Guid.NewGuid(), Code = "advanced", Name = "Расширенный", MonthlyPrice = 990, AnnualPrice = 9900, ActiveListingLimit = 50 });
            db.PromotionProducts.AddRange(
                new Marketplace.Web.Modules.Monetization.PromotionProduct { Id = Guid.NewGuid(), Code = "highlight-7", Name = "Выделение", Description = "Заметное оформление карточки на 7 дней.", Price = 199, DurationDays = 7 },
                new Marketplace.Web.Modules.Monetization.PromotionProduct { Id = Guid.NewGuid(), Code = "boost-7", Name = "Поднятие", Description = "Приоритет в rule-based выдаче на 7 дней.", Price = 299, DurationDays = 7 },
                new Marketplace.Web.Modules.Monetization.PromotionProduct { Id = Guid.NewGuid(), Code = "top-3", Name = "Верхний блок", Description = "Размещение в отдельном верхнем блоке на 3 дня.", Price = 499, DurationDays = 3 });
            db.PromoCodes.Add(new Marketplace.Web.Modules.Monetization.PromoCode { Id = Guid.NewGuid(), Code = "WELCOME20", DiscountPercent = 20, BonusAmount = 100, MaxUses = 1000, ExpiresAt = DateTimeOffset.UtcNow.AddYears(1) });
            await db.SaveChangesAsync();
        }
        await CatalogSeed.InitializeAsync(db);
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        if (environment.IsDevelopment()) await DevelopmentListingSeed.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }
}
