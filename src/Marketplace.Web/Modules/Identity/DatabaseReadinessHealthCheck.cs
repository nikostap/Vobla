using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Marketplace.Web.Modules.Identity;

public sealed class DatabaseReadinessHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var source = configuration.GetConnectionString("Marketplace") ?? throw new InvalidOperationException("Marketplace connection string is missing.");
            var connectionString = new NpgsqlConnectionStringBuilder(source) { Timeout = 2, CommandTimeout = 2 }.ConnectionString;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMilliseconds(2500));
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(deadline.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(deadline.Token);
            return HealthCheckResult.Healthy("PostgreSQL доступен.");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Ошибка подключения к PostgreSQL.", ex); }
    }
}
