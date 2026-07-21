using System.ComponentModel.DataAnnotations;
using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "Administrator")]
public sealed class CatalogModel(MarketplaceDbContext db) : PageModel
{
    public sealed class NewCategoryInput
    {
        [Required, StringLength(160)] public string Name { get; set; } = string.Empty;
        [Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
    }
    public sealed record CategoryListItem(Guid Id, string Name, int? Version);
    [BindProperty] public NewCategoryInput NewCategory { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }
    public IReadOnlyList<CatalogCategory> Categories { get; private set; } = [];
    public IReadOnlyList<CategoryListItem> CategoriesWithSchemas { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateCategoryAsync()
    {
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        var exists = await db.CatalogCategories.AnyAsync(x => x.Slug == NewCategory.Slug);
        if (exists) { ModelState.AddModelError("NewCategory.Slug", "Этот технический код уже используется."); await LoadAsync(); return Page(); }
        if (NewCategory.ParentId is not null && !await db.CatalogCategories.AnyAsync(x => x.Id == NewCategory.ParentId)) { ModelState.AddModelError("NewCategory.ParentId", "Родительская категория не найдена."); await LoadAsync(); return Page(); }
        var category = new CatalogCategory { Id = Guid.NewGuid(), Name = NewCategory.Name.Trim(), Slug = NewCategory.Slug.Trim(), ParentId = NewCategory.ParentId, SortOrder = await db.CatalogCategories.CountAsync() };
        db.CatalogCategories.Add(category);
        db.AuditEvents.Add(new AuditEvent { UserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? Guid.Empty.ToString()), EventType = "catalog.category_created", Detail = $"Создана категория {category.Name}.", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync(); StatusMessage = $"Категория «{category.Name}» добавлена.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateVersionAsync(Guid categoryId)
    {
        var category = await db.CatalogCategories.Include(x => x.SchemaVersions).ThenInclude(x => x.Attributes).FirstOrDefaultAsync(x => x.Id == categoryId);
        if (category is null) return NotFound();
        var current = category.SchemaVersions.Where(x => x.IsPublished).OrderByDescending(x => x.Version).FirstOrDefault();
        var next = new CategorySchemaVersion { Id = Guid.NewGuid(), CategoryId = category.Id, Version = (current?.Version ?? 0) + 1, IsPublished = true };
        db.CategorySchemaVersions.Add(next);
        if (current is not null) db.CategoryAttributes.AddRange(current.Attributes.Select(x => new CategoryAttribute { Id = Guid.NewGuid(), SchemaVersionId = next.Id, Code = x.Code, Name = x.Name, DataType = x.DataType, IsRequired = x.IsRequired, IsFilterable = x.IsFilterable, IsComparable = x.IsComparable, Unit = x.Unit, OptionsJson = x.OptionsJson, SortOrder = x.SortOrder }));
        db.AuditEvents.Add(new AuditEvent { EventType = "catalog.schema_version_created", Detail = $"Создана версия {next.Version} схемы {category.Name}.", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync(); StatusMessage = $"Версия {next.Version} схемы создана.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Categories = await db.CatalogCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        CategoriesWithSchemas = await db.CatalogCategories.AsNoTracking().OrderBy(x => x.Name).Select(x => new CategoryListItem(x.Id, x.Name, x.SchemaVersions.Where(s => s.IsPublished).Select(s => (int?)s.Version).Max())).ToListAsync();
    }
}
