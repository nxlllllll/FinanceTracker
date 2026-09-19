using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace FinanceTracker.Worker.Shared.HealthCheck;

/// <summary>
/// Reports whether RabbitMQ accepts connections. Keeps one connection open between runs, so a scrape
/// every few seconds does not open and close a connection on the broker each time.
/// </summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnectionFactory connectionFactory) : IHealthCheck, IAsyncDisposable
{
	private readonly SemaphoreSlim _lock = new SemaphoreSlim(initialCount: 1, maxCount: 1);
	private IConnection? _connection;

	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken ct = default)
	{
		await _lock.WaitAsync(cancellationToken: ct);

		try
		{
			if (_connection is { IsOpen: true })
				return HealthCheckResult.Healthy();

			IConnection connection = await connectionFactory.CreateConnectionAsync(ct: ct);

			if (_connection is not null)
				await _connection.DisposeAsync();

			_connection = connection;

			return HealthCheckResult.Healthy();
		}
		catch (Exception ex)
		{
			return HealthCheckResult.Unhealthy(description: $"RabbitMQ connection failed: {ex.Message}");
		}
		finally
		{
			_lock.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_connection is not null)
			await _connection.DisposeAsync();

		_lock.Dispose();
	}
}
