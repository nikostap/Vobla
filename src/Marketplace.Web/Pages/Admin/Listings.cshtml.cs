using System.Security.Claims;
using Marketplace.Web.Modules.Identity;
using Marketplace.Web.Modules.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Pages.Admin;

[Authorize(Roles = "Administrator,SeniorAdministrator,Owner,Moderator,SeniorModerator")]
public sealed class ListingsModel(MarketplaceDbContext db) : PageModel
{
    public sealed record ListingView(Guid Id, string Title, string Category, string Owner, string Status, decimal? Price, DateTimeOffset UpdatedAt);
    public IReadOnlyList<ListingView> Listings { get; private set; } = [];
    public async Task OnGetAsync(string? status, string? q) { var query = db.Listings.AsNoTracking(); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status); if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Title.Contains(q)); Listings = await query.OrderByDescending(x => x.UpdatedAt).Take(100).Select(x => new ListingView(x.Id, x.Title, x.Category.Name, x.Owner.Email ?? x.Owner.DisplayName, x.Status, x.Price, x.UpdatedAt)).ToListAsync(); }

    public async Task<IActionResult> OnPostStatusAsync(Guid id, string status, string reason)
    {
        if (status is not ("Active" or "Quarantine" or "Rejected" or "Deleted") || string.IsNullOrWhiteSpace(reason)) return RedirectToPage();
        var listing = await db.Listings.FirstOrDefaultAsync(x => x.Id == id); if (listing is null) return NotFound(); var before = listing.Status; listing.Status = status; listing.UpdatedAt = DateTimeOffset.UtcNow;
        db.ListingStatusHistory.Add(new ListingStatusHistory { Id = Guid.NewGuid(), ListingId = id, Status = status, Reason = reason.Trim() });
        db.AuditEvents.Add(new AuditEvent { UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), EventType = "admin.listing.status", Detail = $"{before} -> {status}: {listing.Title}", ActorRole = User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role)?.Value, EntityType = "Listing", EntityId = id.ToString(), OldValue = before, NewValue = status, Reason = reason.Trim(), CorrelationId = HttpContext.TraceIdentifier, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown" });
        await db.SaveChangesAsync(); return RedirectToPage();
    }
}
