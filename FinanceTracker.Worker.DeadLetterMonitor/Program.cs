using FinanceTracker.Worker.DeadLetterMonitor.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.DeadLetterMonitor;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		UnresolvableEventGaugeOptions gaugeOptions = builder.AddValidatedOptions<UnresolvableEventGaugeOptions>(sectionName: UnresolvableEventGaugeOptions.SectionName);
		DeadLetterBacklogSummaryOptions backlogSummaryOptions = builder.AddValidatedOptions<DeadLetterBacklogSummaryOptions>(sectionName: DeadLetterBacklogSummaryOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz =>
		{
			quartz.AddIntervalJob<UnresolvableEventGaugeJob>(
				group: gaugeOptions.Group,
				triggerName: gaugeOptions.TriggerName,
				interval: TimeSpan.FromMinutes(value: gaugeOptions.IntervalMinutes)
			);

			quartz.AddIntervalJob<DeadLetterBacklogSummaryJob>(
				group: backlogSummaryOptions.Group,
				triggerName: backlogSummaryOptions.TriggerName,
				interval: TimeSpan.FromMinutes(value: backlogSummaryOptions.IntervalMinutes)
			);
		});

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
