namespace Marketplace.Web.Modules.Catalog;

public sealed record ListingCard(
    string Id,
    string Category,
    string Title,
    decimal Price,
    string PriceLabel,
    string Description,
    string Location,
    string PublishedLabel,
    bool VerifiedIdentity,
    bool Negotiable,
    string Image,
    IReadOnlyList<double> Coordinates);
