namespace Marketplace.Web.Modules.Catalog;

public sealed class CatalogCategory
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public CatalogCategory? Parent { get; set; }
    public List<CatalogCategory> Children { get; set; } = [];
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CategorySchemaVersion> SchemaVersions { get; set; } = [];
}
