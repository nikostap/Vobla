namespace Marketplace.Web.Modules.Catalog;

public interface IListingCatalog
{
    Task<IReadOnlyList<ListingCard>> GetFeaturedAsync(CancellationToken cancellationToken);
    Task<CatalogSearchResult> SearchAsync(CatalogSearchRequest request, CancellationToken cancellationToken);
}
