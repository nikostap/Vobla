namespace Marketplace.Web.Modules.Identity;

public sealed class AccountErasureRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ResolvedById { get; set; }
    public string? ResolutionNote { get; set; }
}
