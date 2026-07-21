namespace Marketplace.Web.Modules.Geo;

public static class GeoPrivacy
{
    private static readonly IReadOnlyDictionary<string, (decimal Latitude, decimal Longitude)> CityCenters =
        new Dictionary<string, (decimal, decimal)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Москва"] = (55.751244m, 37.618423m),
            ["Санкт-Петербург"] = (59.934280m, 30.335099m),
            ["Казань"] = (55.796127m, 49.106405m),
            ["Екатеринбург"] = (56.838011m, 60.597465m),
            ["Новосибирск"] = (55.008353m, 82.935733m)
        };

    public static IReadOnlyCollection<string> SupportedCities => CityCenters.Keys.ToArray();

    public static (decimal Latitude, decimal Longitude) CreateStablePublicPoint(Guid listingId, string? city)
    {
        var center = CityCenters.GetValueOrDefault(city ?? "Москва", CityCenters["Москва"]);
        var bytes = listingId.ToByteArray();
        var latitudeOffset = (bytes[0] / 255m - .5m) * .08m;
        var longitudeOffset = (bytes[1] / 255m - .5m) * .12m;
        return (center.Latitude + latitudeOffset, center.Longitude + longitudeOffset);
    }

    public static (decimal Latitude, decimal Longitude) CreateStablePublicPoint(Guid listingId, decimal cityLatitude, decimal cityLongitude)
    {
        var bytes = listingId.ToByteArray();
        return (cityLatitude + (bytes[0] / 255m - .5m) * .08m, cityLongitude + (bytes[1] / 255m - .5m) * .12m);
    }

    public static (decimal Latitude, decimal Longitude) CreatePublicPointFromExact(Guid listingId, decimal latitude, decimal longitude)
    {
        var bytes = listingId.ToByteArray();
        var distanceMeters = 120d + bytes[2] / 255d * 180d;
        var bearing = bytes[3] / 255d * Math.PI * 2d;
        var latitudeOffset = Math.Cos(bearing) * distanceMeters / 111_320d;
        var longitudeScale = Math.Max(.2d, Math.Cos((double)latitude * Math.PI / 180d));
        var longitudeOffset = Math.Sin(bearing) * distanceMeters / (111_320d * longitudeScale);
        return (latitude + (decimal)latitudeOffset, longitude + (decimal)longitudeOffset);
    }

    public static string ResolveNearestCity(double latitude, double longitude) => CityCenters
        .OrderBy(x => Math.Pow((double)x.Value.Latitude - latitude, 2) + Math.Pow((double)x.Value.Longitude - longitude, 2))
        .First().Key;

    public static (decimal Latitude, decimal Longitude) GetCityCenter(string? city) => CityCenters.GetValueOrDefault(city ?? "Москва", CityCenters["Москва"]);

    public static double DistanceKm(double latitudeA, double longitudeA, double latitudeB, double longitudeB)
    {
        const double earthRadius = 6371;
        var dLat = (latitudeB - latitudeA) * Math.PI / 180;
        var dLon = (longitudeB - longitudeA) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(latitudeA * Math.PI / 180) * Math.Cos(latitudeB * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
