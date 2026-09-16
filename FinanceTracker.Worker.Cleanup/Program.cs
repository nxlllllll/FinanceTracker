using FinanceTracker.Worker.Cleanup.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.Cleanup;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		CleanupOptions cleanupOptions = builder.AddValidatedOptions<CleanupOptions>(sectionName: CleanupOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddIntervalJob<CleanupJob>(
			group: cleanupOptions.Group,
			triggerName: cleanupOptions.TriggerName,
			interval: TimeSpan.FromMinutes(value: cleanupOptions.IntervalMinutes)
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
