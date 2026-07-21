namespace Marketplace.Web.Modules.Catalog;

public sealed class CategoryAttribute
{
    public Guid Id { get; set; }
    public Guid SchemaVersionId { get; set; }
    public CategorySchemaVersion SchemaVersion { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = "string";
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public bool IsComparable { get; set; }
    public string? Unit { get; set; }
    public string? OptionsJson { get; set; }
    public int SortOrder { get; set; }
}
