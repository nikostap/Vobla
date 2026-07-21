namespace Marketplace.Web.Modules.Identity;

public sealed class OneTimeCodeChallenge
{
    public Guid Id { get; set; }
    public string NormalizedEmail { get; set; } = string.Empty;
    public byte[] CodeHash { get; set; } = [];
    public byte[] Salt { get; set; } = [];
    public string? DebugCode { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset ResendAvailableAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
