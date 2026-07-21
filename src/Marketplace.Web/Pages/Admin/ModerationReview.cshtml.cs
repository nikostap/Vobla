using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Listings;
using Marketplace.Web.Modules.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;
[Authorize(Roles = "Moderator,Administrator")]
public sealed class ModerationReviewModel(MarketplaceDbContext db) : PageModel
{
    public sealed class DecisionInput { [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty; [StringLength(120)] public string? ProblemField { get; set; } [StringLength(300)] public string RuleReference { get; set; } = "rules/community"; [StringLength(1000)] public string? Instruction { get; set; } }
    public sealed record FindingView(string Code, string Risk, string? Fragment, string RecommendedAction, string RuleCode, string RuleVersion, decimal Confidence);
    public sealed record AppealView(string Message, DateTimeOffset CreatedAt);
    public sealed record CaseView(Guid Id, string Title, string? Description, string Category, string OwnerName, string PriceLabel, string RiskLevel, string RuleSetVersion, List<FindingView> Findings, List<AppealView> Appeals);
    [BindProperty] public DecisionInput Input { get; set; } = new(); public CaseView? Case { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid id) { if (!await LoadAsync(id)) return NotFound(); return Page(); }
    public async Task<IActionResult> OnPostAsync(Guid id, string action) { if (!ModelState.IsValid) { await LoadAsync(id); return Page(); } var moderationCase = await db.ModerationCases.Include(x => x.Listing).Include(x => x.Appeals).FirstOrDefaultAsync(x => x.Id == id && (x.Status == "Pending" || x.Status == "Appealed")); if (moderationCase is null) return NotFound(); var moderatorId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!); var targetStatus = action switch { "Approve" => "Active", "NeedsCorrection" => "NeedsCorrection", "Reject" => "Rejected", "Escalate" => "Quarantine", _ => "PendingManualReview" }; moderationCase.Status = action == "Escalate" ? "Escalated" : "Resolved"; moderationCase.ResolvedAt = DateTimeOffset.UtcNow; moderationCase.AssignedModeratorId = moderatorId; moderationCase.Listing.Status = targetStatus; foreach (var appeal in moderationCase.Appeals.Where(x => x.Status == "Pending")) appeal.Status = action == "Approve" ? "Accepted" : "Resolved"; db.ModerationDecisions.Add(new ModerationDecision { Id = Guid.NewGuid(), ModerationCaseId = id, ModeratorId = moderatorId, Action = action, Reason = Input.Reason.Trim(), ProblemField = Input.ProblemField?.Trim(), RuleReference = Input.RuleReference.Trim(), CorrectionInstruction = Input.Instruction?.Trim() ?? string.Empty }); db.ListingStatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = moderationCase.ListingId, Status = targetStatus, Reason = Input.Reason.Trim() }); db.AuditEvents.Add(new AuditEvent { UserId = moderatorId, EventType = "moderation.decision", Detail = $"{action}: {moderationCase.Listing.Title}", IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" }); await db.SaveChangesAsync(); return RedirectToPage("/Admin/Moderation"); }
    private async Task<bool> LoadAsync(Guid id) { var item = await db.ModerationCases.AsNoTracking().Include(x => x.Listing).ThenInclude(x => x.Category).Include(x => x.Listing).ThenInclude(x => x.Owner).Include(x => x.Findings).Include(x => x.Appeals).FirstOrDefaultAsync(x => x.Id == id); if (item is null) return false; Case = new CaseView(item.Id, item.Listing.Title, item.Listing.Description, item.Listing.Category.Name, item.Listing.Owner.DisplayName, item.Listing.DealType == "Free" ? "Бесплатно" : item.Listing.Price == null ? "Цена не указана" : $"{item.Listing.Price:N0} ₽", item.RiskLevel, item.RuleSetVersion, item.Findings.Select(x => new FindingView(x.Code, x.RiskCategory, x.Fragment, x.RecommendedAction, x.RuleCode, x.RuleVersion, x.Confidence)).ToList(), item.Appeals.Where(x => x.Status == "Pending").OrderByDescending(x => x.CreatedAt).Select(x => new AppealView(x.Message, x.CreatedAt)).ToList()); return true; }
}
