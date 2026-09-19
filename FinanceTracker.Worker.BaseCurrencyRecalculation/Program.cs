using FinanceTracker.Worker.BaseCurrencyRecalculation.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.BaseCurrencyRecalculation;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		BaseCurrencyRecalculationJobOptions jobOptions = builder.AddValidatedOptions<BaseCurrencyRecalculationJobOptions>(sectionName: BaseCurrencyRecalculationJobOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddCronJob<BaseCurrencyRecalculationJob>(
			group: jobOptions.Group,
			triggerName: jobOptions.TriggerName,
			cronExpression: jobOptions.CronExpression
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
