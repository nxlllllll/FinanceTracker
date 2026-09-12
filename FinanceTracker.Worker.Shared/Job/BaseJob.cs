using FinanceTracker.Worker.Shared.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using RabbitMQ.Client.Exceptions;
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
		string jobName = GetType().Name;
		TOptions currentOptions = options.CurrentValue;

		if (!currentOptions.IsEnabled)
		{
			logger.ZLogInformation(message: $"[{jobName}] Disabled. Skipping.");
			return;
		}

		try
		{
			await ProcessAsync(options: currentOptions, ct: context.CancellationToken);
		}
		catch (Exception ex) when (IsDependencyUnavailable(exception: ex))
		{
			WorkerMetrics.JobExecutionSkipped.Add(delta: 1, new KeyValuePair<string, object?>(key: "job", value: jobName));
			logger.ZLogWarning(exception: ex, message: $"[{jobName}] A dependency is unavailable. Skipping this run.");
		}
		catch (Exception ex)
		{
			WorkerMetrics.JobExecutionFailed.Add(delta: 1, new KeyValuePair<string, object?>(key: "job", value: GetType().Name));
			logger.ZLogError(exception: ex, message: $"[{jobName}] Unhandled exception during execution.");
			throw new JobExecutionException(cause: ex, refireImmediately: false);
		}
	}

	protected static bool IsDependencyUnavailable(Exception exception) => exception switch
	{
		BrokerUnreachableException unreachable => !IsRefusedByBroker(exception: unreachable.InnerException),
		AlreadyClosedException => true,
		_ => false
	};

	private static bool IsRefusedByBroker(Exception? exception)
	{
		for (Exception? current = exception; current is not null; current = current.InnerException)
		{
			if (current is AuthenticationFailureException or OperationInterruptedException)
				return true;
		}

		return false;
	}

	protected abstract Task ProcessAsync(TOptions options, CancellationToken ct);
}
