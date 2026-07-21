using System.Text.Json;
using Marketplace.Web.Modules.Catalog;
using Marketplace.Web.Modules.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages;

public sealed class CatalogModel(MarketplaceDbContext db) : PageModel
{
    public sealed record CategoryNode(string Slug, string Name, List<CategoryNode> Children, bool ContainsSelection = false);
    public sealed record AttributeView(string Name, string DataType, bool IsRequired, string? Unit, List<string> Options);
    public sealed record SchemaView(string CategoryName, int Version, List<AttributeView> Attributes);
    public IReadOnlyList<CategoryNode> Roots { get; private set; } = [];
    public SchemaView? Selected { get; private set; }
    public string? SelectedSlug { get; private set; }

    public async Task OnGetAsync(string? slug)
    {
        SelectedSlug = slug;
        var categories = await db.CatalogCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToListAsync();
        Roots = BuildTree(categories, null, slug);
        if (string.IsNullOrWhiteSpace(slug)) return;
        var version = await db.CategorySchemaVersions.AsNoTracking().Include(x => x.Category).Include(x => x.Attributes).FirstOrDefaultAsync(x => x.Category.Slug == slug && x.IsPublished);
        if (version is null) return;
        Selected = new SchemaView(version.Category.Name, version.Version, version.Attributes.OrderBy(x => x.SortOrder).Select(x => new AttributeView(x.Name, x.DataType, x.IsRequired, x.Unit, string.IsNullOrWhiteSpace(x.OptionsJson) ? [] : JsonSerializer.Deserialize<List<string>>(x.OptionsJson) ?? [])).ToList());
    }

    private static List<CategoryNode> BuildTree(List<CatalogCategory> source, Guid? parentId, string? selected) => source.Where(x => x.ParentId == parentId).Select(x =>
    {
        var children = BuildTree(source, x.Id, selected);
        return new CategoryNode(x.Slug, x.Name, children, x.Slug == selected || children.Any(c => c.ContainsSelection));
    }).ToList();
}
