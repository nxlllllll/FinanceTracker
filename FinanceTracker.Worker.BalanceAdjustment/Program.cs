using FinanceTracker.Worker.BalanceAdjustment.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.BalanceAdjustment;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		BalanceAdjustmentJobOptions jobOptions = builder.AddValidatedOptions<BalanceAdjustmentJobOptions>(sectionName: BalanceAdjustmentJobOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddCronJob<BalanceAdjustmentJob>(
			group: jobOptions.Group,
			triggerName: jobOptions.TriggerName,
			cronExpression: jobOptions.CronExpression
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
