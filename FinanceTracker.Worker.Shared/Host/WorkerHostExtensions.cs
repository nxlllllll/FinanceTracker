using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Worker.Shared.Host;

public static class WorkerHostExtensions
{
	public static WebApplicationBuilder AddWorkerDefaults(this WebApplicationBuilder builder)
	{
		string workerName = builder.Environment.ApplicationName;

		builder.AddStructuredLogging();
		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services.AddWorkerHealthChecks(
			connectionString: builder.Configuration.RequireConnectionString(name: nameof(FinanceTrackerContext)),
			redisConnectionString: builder.Configuration.RequireValue(path: ConfigurationPath.Combine(RedisOptions.SectionName, nameof(RedisOptions.ConnectionString)))
		);

		builder.Services.AddWorkerMetrics(workerName: workerName);
		builder.Services.AddWorkerTracing(workerName: workerName);

		return builder;
	}

	public static TOptions AddValidatedOptions<TOptions>(this WebApplicationBuilder builder, string sectionName) where TOptions : class, new()
	{
		builder.Services.AddOptions<TOptions>()
			.BindConfiguration(configSectionPath: sectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		return builder.Configuration.GetSection(key: sectionName).Get<TOptions>() ?? new TOptions();
	}
}
