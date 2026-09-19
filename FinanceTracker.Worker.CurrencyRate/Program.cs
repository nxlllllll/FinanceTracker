using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.CurrencyRate.Client;
using FinanceTracker.Worker.CurrencyRate.HealthCheck;
using FinanceTracker.Worker.CurrencyRate.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using ZLogger;

namespace FinanceTracker.Worker.CurrencyRate;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		ExchangeRateApiOptions apiOptions = builder.AddValidatedOptions<ExchangeRateApiOptions>(sectionName: ExchangeRateApiOptions.SectionName);

		builder.Services.AddSingleton<CircuitBreakerStateProvider>();

		builder.Services.AddHttpClient<ExchangeRateApiClient>().AddResilienceHandler(pipelineName: nameof(ExchangeRateApiClient), configure: (pipeline, context) =>
		{
			CircuitBreakerStateProvider stateProvider = context.ServiceProvider.GetRequiredService<CircuitBreakerStateProvider>();
			ILogger<ExchangeRateApiClient> resilienceLogger = context.ServiceProvider.GetRequiredService<ILogger<ExchangeRateApiClient>>();

			pipeline.AddTimeout(timeout: TimeSpan.FromSeconds(value: apiOptions.TotalTimeoutSeconds));

			pipeline.AddRetry(options: new HttpRetryStrategyOptions
			{
				MaxRetryAttempts = apiOptions.RetryCount,
				Delay = TimeSpan.FromSeconds(value: apiOptions.RetryDelaySeconds),
				BackoffType = DelayBackoffType.Exponential,
				UseJitter = true,
				OnRetry = onRetryArguments =>
				{
					resilienceLogger.ZLogWarning(message: $"""
						[ExchangeRateApi] Retry {onRetryArguments.AttemptNumber + 1}/{apiOptions.RetryCount} after {onRetryArguments.RetryDelay.TotalMilliseconds}ms.
						Reason: {onRetryArguments.Outcome.Exception?.Message ?? "non-success status"}.
					""");

					return ValueTask.CompletedTask;
				}
			});

			pipeline.AddCircuitBreaker(options: new HttpCircuitBreakerStrategyOptions
			{
				FailureRatio = apiOptions.CircuitBreakerFailureRatio,
				MinimumThroughput = apiOptions.CircuitBreakerMinThroughput,
				SamplingDuration = TimeSpan.FromSeconds(value: apiOptions.CircuitBreakerSamplingSeconds),
				BreakDuration = TimeSpan.FromSeconds(value: apiOptions.CircuitBreakerBreakSeconds),
				StateProvider = stateProvider,
				OnOpened = onCircuitOpenedArguments =>
				{
					resilienceLogger.ZLogError(message: $"""
						[ExchangeRateApi] Circuit OPENED for {apiOptions.CircuitBreakerBreakSeconds}s.
						Reason: {onCircuitOpenedArguments.Outcome.Exception?.Message ?? "failure ratio exceeded"}.
					""");

					return ValueTask.CompletedTask;
				},
				OnClosed = _ =>
				{
					resilienceLogger.ZLogInformation(message: $"[ExchangeRateApi] Circuit CLOSED — service recovered.");

					return ValueTask.CompletedTask;
				},
				OnHalfOpened = _ =>
				{
					resilienceLogger.ZLogInformation(message: $"[ExchangeRateApi] Circuit HALF-OPEN — probing service.");

					return ValueTask.CompletedTask;
				}
			});

			pipeline.AddTimeout(timeout: TimeSpan.FromSeconds(value: apiOptions.TimeoutSeconds));
		});

		builder.Services.AddHealthChecks().AddCheck<ExchangeRateApiHealthCheck>(
			name: ExchangeRateApiHealthCheck.Name,
			tags: [HealthCheckTags.Ready, HealthCheckTags.External]
		);

		CurrencyRateJobOptions jobOptions = builder.AddValidatedOptions<CurrencyRateJobOptions>(sectionName: CurrencyRateJobOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddCronJob<CurrencyRateJob>(
			group: jobOptions.Group,
			triggerName: jobOptions.TriggerName,
			cronExpression: jobOptions.CronExpression
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
