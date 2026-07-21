using Marketplace.Web.Modules.Catalog;

namespace Marketplace.Web.Modules.Geo;

public sealed record MapFeature(string Id, string Label, string Image, int Count, double X, double Y, bool IsCluster);

public interface IMapProviderAdapter
{
    string ProviderName { get; }
    IReadOnlyList<MapFeature> BuildFeatures(IReadOnlyList<ListingCard> listings, int zoom);
}

public sealed class DemoYandexMapAdapter : IMapProviderAdapter
{
    public string ProviderName => "YandexMapsAdapter:demo";

    public IReadOnlyList<MapFeature> BuildFeatures(IReadOnlyList<ListingCard> listings, int zoom)
    {
        if (listings.Count == 0) return [];
        var points = listings.Select(x => new { Listing = x, Lat = x.Coordinates.ElementAtOrDefault(0), Lon = x.Coordinates.ElementAtOrDefault(1) }).ToList();
        var precision = zoom >= 14 ? 1000d : zoom >= 11 ? 25d : 4d;
        var groups = points.GroupBy(x => (Lat: Math.Round(x.Lat * precision) / precision, Lon: Math.Round(x.Lon * precision) / precision)).ToList();
        var minLat = groups.Min(x => x.Average(p => p.Lat)); var maxLat = groups.Max(x => x.Average(p => p.Lat));
        var minLon = groups.Min(x => x.Average(p => p.Lon)); var maxLon = groups.Max(x => x.Average(p => p.Lon));
        return groups.Select((group, index) =>
        {
            var first = group.First().Listing; var lat = group.Average(x => x.Lat); var lon = group.Average(x => x.Lon);
            var x = maxLon == minLon ? 50 : 12 + (lon - minLon) / (maxLon - minLon) * 76;
            var y = maxLat == minLat ? 50 : 12 + (maxLat - lat) / (maxLat - minLat) * 76;
            return new MapFeature(group.Count() == 1 ? first.Id : $"cluster-{index}", group.Count() == 1 ? first.PriceLabel : $"{group.Count()} объявлений", first.Image, group.Count(), x, y, group.Count() > 1);
        }).ToList();
    }
}
