using Microsoft.AspNetCore.Identity;

namespace Marketplace.Web.Modules.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string City { get; set; } = "Москва";
    public string? Address { get; set; }
    public decimal? CityLatitude { get; set; }
    public decimal? CityLongitude { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public bool ShowPhone { get; set; }
    public bool ShowEmail { get; set; }
    public bool AllowMessages { get; set; } = true;
    public bool ShowExactAddress { get; set; }
    public bool ShowOnlineStatus { get; set; } = true;
    public bool UseHistoryForRecommendations { get; set; } = true;
    public bool EmailNotifications { get; set; } = true;
}
