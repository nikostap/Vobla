namespace Marketplace.Web.Modules.Identity;

public sealed class AuthRateLimitBucket
{
    public string Key { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
