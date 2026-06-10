using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.HealthCheck;

/// <summary>
/// ASP.NET Core health check that verifies RabbitMQ connectivity by attempting
/// to open and immediately close a connection. Reports <c>Unhealthy</c> on failure.
/// </summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnectionFactory connectionFactory) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
	{
		try
		{
			await using IConnection connection = await connectionFactory.CreateConnectionAsync(ct: ct);
			return HealthCheckResult.Healthy();
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(description: $"RabbitMQ connection failed: {ex.Message}");
		}
	}
}
