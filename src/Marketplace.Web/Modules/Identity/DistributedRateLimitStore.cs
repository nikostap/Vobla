using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Modules.Identity;

public sealed class DistributedRateLimitStore(MarketplaceDbContext db)
{
    public async Task<int> IncrementAsync(string scope, string subject, DateTimeOffset windowStart, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{scope}|{subject}|{windowStart.ToUnixTimeSeconds()}")));
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT INTO "AuthRateLimitBuckets" ("Key", "Count", "ExpiresAt")
            VALUES (@key, 1, @expiresAt)
            ON CONFLICT ("Key") DO UPDATE
            SET "Count" = "AuthRateLimitBuckets"."Count" + 1,
                "ExpiresAt" = EXCLUDED."ExpiresAt"
            RETURNING "Count";
            """;
        AddParameter(command, "@key", key);
        AddParameter(command, "@expiresAt", expiresAt);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter);
    }
}
