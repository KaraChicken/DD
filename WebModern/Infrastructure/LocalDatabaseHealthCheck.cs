using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DDTank.Web.Infrastructure
{
    /// <summary>Reports whether the local SQLite database and character reference seed are ready.</summary>
    public sealed class LocalDatabaseHealthCheck : IHealthCheck
    {
        private readonly LocalDatabaseRuntime _runtime;

        /// <summary>Creates a health check over the initialized local database.</summary>
        public LocalDatabaseHealthCheck(LocalDatabaseRuntime runtime)
        {
            _runtime = runtime;
        }

        /// <inheritdoc />
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_runtime.IsReady(out var description)
                ? HealthCheckResult.Healthy(description)
                : HealthCheckResult.Unhealthy(description));
        }
    }
}
