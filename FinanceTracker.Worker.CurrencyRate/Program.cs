using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.CurrencyRate.Client;
using FinanceTracker.Worker.CurrencyRate.HealthCheck;
using FinanceTracker.Worker.CurrencyRate.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Quartz;
using ZLogger;

namespace FinanceTracker.Worker.CurrencyRate;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services
			.AddOptions<ExchangeRateApiOptions>()
			.BindConfiguration(configSectionPath: ExchangeRateApiOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		ExchangeRateApiOptions apiOptions = builder.Configuration
			.GetSection(key: ExchangeRateApiOptions.SectionName)
			.Get<ExchangeRateApiOptions>() ?? new ExchangeRateApiOptions();

		builder.Services.AddSingleton<CircuitBreakerStateProvider>();

		builder.Services.AddHttpClient<ExchangeRateApiClient>().AddResilienceHandler(pipelineName: "exchange-rate-api", configure: (pipeline, context) =>
		{
			CircuitBreakerStateProvider stateProvider = context.ServiceProvider.GetRequiredService<CircuitBreakerStateProvider>();
			ILogger<ExchangeRateApiClient> resilienceLogger = context.ServiceProvider.GetRequiredService<ILogger<ExchangeRateApiClient>>();

			pipeline.AddTimeout(timeout: TimeSpan.FromSeconds(value: apiOptions.TimeoutSeconds));

			pipeline.AddRetry(options: new HttpRetryStrategyOptions
			{
				MaxRetryAttempts = apiOptions.RetryCount,
				Delay = TimeSpan.FromSeconds(value: apiOptions.RetryDelaySeconds),
				BackoffType = DelayBackoffType.Exponential,
				UseJitter = true,
				OnRetry = async onRetryArguments => resilienceLogger.ZLogWarning(message: $"""
					[ExchangeRateApi] Retry {onRetryArguments.AttemptNumber + 1}/{apiOptions.RetryCount} after {onRetryArguments.RetryDelay.TotalMilliseconds}ms.
					Reason: {onRetryArguments.Outcome.Exception?.Message ?? "non-success status"}.
				""")
			});

			pipeline.AddCircuitBreaker(options: new HttpCircuitBreakerStrategyOptions
			{
				FailureRatio = apiOptions.CircuitBreakerFailureRatio,
				MinimumThroughput = apiOptions.CircuitBreakerMinThroughput,
				SamplingDuration = TimeSpan.FromSeconds(value: apiOptions.CircuitBreakerSamplingSeconds),
				BreakDuration = TimeSpan.FromSeconds(value: apiOptions.CircuitBreakerBreakSeconds),
				StateProvider = stateProvider,
				OnOpened = async onCircuitOpenedArguments => resilienceLogger.ZLogError(message: $"""
					[ExchangeRateApi] Circuit OPENED for {apiOptions.CircuitBreakerBreakSeconds}s. 
					Reason: {onCircuitOpenedArguments.Outcome.Exception?.Message ?? "failure ratio exceeded"}.
				"""),
				OnClosed = async _ => resilienceLogger.ZLogInformation(message: $"[ExchangeRateApi] Circuit CLOSED — service recovered."),
				OnHalfOpened = async _ => resilienceLogger.ZLogInformation(message: $"[ExchangeRateApi] Circuit HALF-OPEN — probing service.")
			});
		});

		CurrencyRateJobOptions jobOptions = builder.Configuration
			.GetSection(key: CurrencyRateJobOptions.SectionName)
			.Get<CurrencyRateJobOptions>() ?? new CurrencyRateJobOptions();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services.AddQuartz(configure: q =>
		{
			q.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: "CurrencyRateScheduler");

			q.AddJob<CurrencyRateJob>(configure: j => j.WithIdentity(name: nameof(CurrencyRateJob), group: jobOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(CurrencyRateJob), jobGroup: jobOptions.Group)
				.WithIdentity(name: jobOptions.TriggerName, group: jobOptions.Group)
				.WithCronSchedule(
					cronExpression: jobOptions.CronExpression,
					schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
				)
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"])
			.AddCheck<ExchangeRateApiHealthCheck>(name: "exchange-rate-api", tags: ["ready", "external"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.CurrencyRate");
		builder.Services.AddWorkerTracing(workerName: "Worker.CurrencyRate");

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
