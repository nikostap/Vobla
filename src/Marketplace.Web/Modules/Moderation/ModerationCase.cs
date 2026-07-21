using Marketplace.Web.Modules.Listings;

namespace Marketplace.Web.Modules.Moderation;

public sealed class ModerationCase
{
    public Guid Id { get; set; }
    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string RiskLevel { get; set; } = "Low";
    public string RuleSetVersion { get; set; } = "demo-1.0";
    public Guid? AssignedModeratorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public List<ModerationFinding> Findings { get; set; } = [];
    public List<ModerationDecision> Decisions { get; set; } = [];
    public List<ModerationAppeal> Appeals { get; set; } = [];
}
