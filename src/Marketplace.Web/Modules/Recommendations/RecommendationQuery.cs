using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Marketplace.Web.Modules.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Marketplace.Web.Modules.Recommendations;

public static class RecommendationQuery
{
    private static readonly HashSet<string> IgnoredKeys = new(StringComparer.OrdinalIgnoreCase) { "mode", "zoom", "handler" };

    public static string Normalize(IEnumerable<KeyValuePair<string, StringValues>> values)
    {
        var pairs = values
            .Where(x => !IgnoredKeys.Contains(x.Key) && !StringValues.IsNullOrEmpty(x.Value))
            .SelectMany(x => x.Value.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => new KeyValuePair<string, string?>(x.Key, v!.Trim())))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return QueryString.Create(pairs).Value ?? string.Empty;
    }

    public static string Fingerprint(string queryString) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(queryString)));

    public static string Label(IReadOnlyDictionary<string, StringValues> values)
    {
        var parts = new List<string>();
        if (values.TryGetValue("q", out var q) && !StringValues.IsNullOrEmpty(q)) parts.Add($"Поиск: {q}");
        if (values.TryGetValue("category", out var category) && !StringValues.IsNullOrEmpty(category)) parts.Add(category.ToString());
        if (values.TryGetValue("address", out var address) && !StringValues.IsNullOrEmpty(address)) parts.Add(address.ToString());
        else if (values.TryGetValue("city", out var city) && !StringValues.IsNullOrEmpty(city)) parts.Add(city.ToString());
        if (values.TryGetValue("minPrice", out var min) && !StringValues.IsNullOrEmpty(min)) parts.Add($"от {min} ₽");
        if (values.TryGetValue("maxPrice", out var max) && !StringValues.IsNullOrEmpty(max)) parts.Add($"до {max} ₽");
        return parts.Count == 0 ? "Все объявления" : string.Join(" · ", parts);
    }

    public static CatalogSearchRequest ToSearchRequest(string queryString)
    {
        var values = QueryHelpers.ParseQuery(queryString);
        static decimal? Decimal(IReadOnlyDictionary<string, StringValues> source, string key) => source.TryGetValue(key, out var value) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        static int Integer(IReadOnlyDictionary<string, StringValues> source, string key, int fallback) => source.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;
        static string? Text(IReadOnlyDictionary<string, StringValues> source, string key) => source.TryGetValue(key, out var value) && !StringValues.IsNullOrEmpty(value) ? value.ToString() : null;
        var attributes = values.Where(x => x.Key.StartsWith("attr_", StringComparison.OrdinalIgnoreCase)).ToDictionary(x => x.Key[5..], x => x.Value.ToString());
        return new CatalogSearchRequest(Text(values, "q"), Text(values, "category"), Decimal(values, "minPrice"), Decimal(values, "maxPrice"), Text(values, "condition"), Text(values, "dealType"), string.Equals(Text(values, "hasPhoto"), "true", StringComparison.OrdinalIgnoreCase), Text(values, "sort") ?? "recommended", attributes, Text(values, "city"), Math.Clamp(Integer(values, "radius", 30), 1, 500), CenterLatitude: Decimal(values, "latitude"), CenterLongitude: Decimal(values, "longitude"));
    }
}
