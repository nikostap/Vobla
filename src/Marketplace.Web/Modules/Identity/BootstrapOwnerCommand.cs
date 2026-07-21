using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Identity;

public static class BootstrapOwnerCommand
{
    private const long AdvisoryLockKey = 4_869_177_001;

    public static void ValidateConfiguration(IConfiguration configuration) => ReadSettings(configuration);

    public static async Task ExecuteAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var (email, displayName) = ReadSettings(configuration);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);

        var existingOwners = await userManager.GetUsersInRoleAsync("Owner");
        var normalizedEmail = userManager.NormalizeEmail(email);
        var user = await userManager.FindByEmailAsync(email);
        if (existingOwners.Any(owner => owner.NormalizedEmail != normalizedEmail))
            throw new InvalidOperationException("An Owner already exists. Use the audited administration workflow instead of bootstrap.");

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                RegisteredAt = DateTimeOffset.UtcNow
            };
            EnsureSucceeded(await userManager.CreateAsync(user), "create bootstrap Owner");
        }

        if (!await userManager.IsInRoleAsync(user, "Member"))
            EnsureSucceeded(await userManager.AddToRoleAsync(user, "Member"), "assign Member role");
        if (!await userManager.IsInRoleAsync(user, "Owner"))
            EnsureSucceeded(await userManager.AddToRoleAsync(user, "Owner"), "assign Owner role");

        if (!await db.AuditEvents.AnyAsync(x => x.UserId == user.Id && x.EventType == "identity.owner.bootstrapped", cancellationToken))
        {
            db.AuditEvents.Add(new AuditEvent
            {
                UserId = user.Id,
                EventType = "identity.owner.bootstrapped",
                Detail = "Initial production Owner provisioned by deployment command.",
                IpAddress = "deployment-command",
                ActorRole = "Bootstrap",
                EntityType = "ApplicationUser",
                EntityId = user.Id.ToString(),
                NewValue = "Owner",
                Reason = "Initial production access provisioning",
                CorrelationId = Guid.NewGuid().ToString("D")
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static bool IsValidEmail(string value)
    {
        try { return new MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }

    private static (string Email, string DisplayName) ReadSettings(IConfiguration configuration)
    {
        var email = configuration["Bootstrap:OwnerEmail"]?.Trim();
        var displayName = configuration["Bootstrap:OwnerDisplayName"]?.Trim();
        var token = configuration["Bootstrap:OwnerToken"];
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            throw new InvalidOperationException("Bootstrap:OwnerToken must be supplied by a secret provider and contain at least 32 characters.");
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
            throw new InvalidOperationException("Bootstrap:OwnerEmail must contain a valid email address.");
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length is < 2 or > 100)
            throw new InvalidOperationException("Bootstrap:OwnerDisplayName must contain between 2 and 100 characters.");
        return (email, displayName);
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException($"Unable to {action}: {string.Join("; ", result.Errors.Select(error => error.Code))}");
    }
}
