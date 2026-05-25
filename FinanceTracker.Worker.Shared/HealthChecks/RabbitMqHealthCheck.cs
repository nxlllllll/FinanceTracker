using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.HealthChecks;

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
