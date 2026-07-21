using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Identity;

public interface IOneTimeCodeService
{
    Task<CodeDeliveryResult> SendAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> VerifyAsync(string email, string code, CancellationToken cancellationToken = default);
}

public sealed record CodeDeliveryResult(bool Succeeded, string? Code, string Message);

public sealed class FakeEmailCodeService(MarketplaceDbContext db) : IOneTimeCodeService
{
    public async Task<CodeDeliveryResult> SendAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Normalize(email);
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockEmailAsync(normalizedEmail, cancellationToken);
        var challenge = await FindForUpdateAsync(normalizedEmail, cancellationToken);
        if (challenge is not null && challenge.ExpiresAt >= now && challenge.ResendAvailableAt > now)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, challenge.DebugCode, "Уже созданный код действует 10 минут; повторная выдача доступна через минуту.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var salt = RandomNumberGenerator.GetBytes(16);
        challenge ??= new OneTimeCodeChallenge { Id = Guid.NewGuid(), NormalizedEmail = normalizedEmail };
        challenge.CodeHash = Hash(code, salt);
        challenge.Salt = salt;
        challenge.DebugCode = code;
        challenge.ExpiresAt = now.AddMinutes(10);
        challenge.ResendAvailableAt = now.AddMinutes(1);
        challenge.Attempts = 0;
        challenge.UpdatedAt = now;
        if (db.Entry(challenge).State == EntityState.Detached) db.OneTimeCodeChallenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, code, "Код создан локальным fake-provider и действует 10 минут.");
    }

    public async Task<bool> VerifyAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Normalize(email);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockEmailAsync(normalizedEmail, cancellationToken);
        var challenge = await FindForUpdateAsync(normalizedEmail, cancellationToken);
        if (challenge is null || challenge.ExpiresAt < DateTimeOffset.UtcNow || challenge.Attempts >= 5)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var suppliedHash = Hash(code.PadLeft(6, '0'), challenge.Salt);
        if (!CryptographicOperations.FixedTimeEquals(challenge.CodeHash, suppliedHash))
        {
            challenge.Attempts++;
            challenge.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        db.OneTimeCodeChallenges.Remove(challenge);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task LockEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({normalizedEmail})::bigint)", cancellationToken);

    private async Task<OneTimeCodeChallenge?> FindForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var query = db.OneTimeCodeChallenges.FromSqlInterpolated($$"""
            SELECT * FROM "OneTimeCodeChallenges"
            WHERE "NormalizedEmail" = {{normalizedEmail}}
            FOR UPDATE
            """).AsAsyncEnumerable();
        await foreach (var challenge in query.WithCancellation(cancellationToken)) return challenge;
        return null;
    }

    private static byte[] Hash(string code, byte[] salt)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var payload = new byte[salt.Length + codeBytes.Length];
        salt.CopyTo(payload, 0);
        codeBytes.CopyTo(payload, salt.Length);
        return SHA256.HashData(payload);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
