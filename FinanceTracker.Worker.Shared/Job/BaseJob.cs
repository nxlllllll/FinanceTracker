using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.Shared.Job;

/// <summary>
/// Base class for all Quartz jobs. Handles the IsEnabled check and logs a
/// standardised disabled message using the concrete job type name.
/// Subclasses implement <see cref="ProcessAsync"/> for the actual work.
/// </summary>
public abstract class BaseJob<TOptions>(
	IOptionsMonitor<TOptions> options,
	ILogger logger
) : IJob where TOptions : class, IJobOptions
{
	public async Task Execute(IJobExecutionContext context)
	{
		TOptions currentOptions = options.CurrentValue;

		if (!currentOptions.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{GetType().Name}] Disabled. Skipping.");
			return;
		}

		await ProcessAsync(options: currentOptions, ct: context.CancellationToken);
	}

	protected abstract Task ProcessAsync(TOptions options, CancellationToken ct);
}