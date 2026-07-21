using System.Net;

namespace Marketplace.Web.Modules.Geo;

public sealed record GeoGuess(string City, string Country, bool IsFallback, string Source);

public interface IIpGeoAdapter
{
    Task<GeoGuess> ResolveAsync(IPAddress? address, CancellationToken cancellationToken);
}

public sealed class LocalIpGeoAdapter : IIpGeoAdapter
{
    public Task<GeoGuess> ResolveAsync(IPAddress? address, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Adapter boundary for a production GeoIP provider. Local/private addresses deliberately fall back to Moscow.
        var fallback = address is null || IPAddress.IsLoopback(address) || address.ToString().StartsWith("10.") || address.ToString().StartsWith("192.168.");
        return Task.FromResult(new GeoGuess("Москва", "Россия", fallback, fallback ? "fallback" : "ip-adapter"));
    }
}
