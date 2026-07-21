namespace Marketplace.Web.Modules.Catalog;

public sealed class CategorySchemaVersion
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public CatalogCategory Category { get; set; } = null!;
    public int Version { get; set; }
    public bool IsPublished { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CategoryAttribute> Attributes { get; set; } = [];
}
