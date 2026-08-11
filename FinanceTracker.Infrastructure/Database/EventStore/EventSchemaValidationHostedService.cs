using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore;

/// <summary>
/// Runs <see cref="EventSchemaCompatibilityValidator"/>
/// once at startup, before the host begins serving
/// </summary>
public sealed class EventSchemaValidationHostedService(
	EventSchemaCompatibilityValidator validator,
	ILogger<EventSchemaValidationHostedService> logger
) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		validator.Validate();
		logger.ZLogDebug(message: $"[Upcasting] Event schema versions are consistent with the registered upcaster chains.");

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
