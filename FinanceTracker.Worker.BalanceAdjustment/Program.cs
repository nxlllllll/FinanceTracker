using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.BalanceAdjustment.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.BalanceAdjustment;

public sealed class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

        builder.Services.AddInfrastructure(configuration: builder.Configuration);

        builder.Services.AddOptions<BalanceAdjustmentJobOptions>()
            .BindConfiguration(configSectionPath: BalanceAdjustmentJobOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        BalanceAdjustmentJobOptions jobOptions = builder.Configuration
            .GetSection(key: BalanceAdjustmentJobOptions.SectionName)
            .Get<BalanceAdjustmentJobOptions>() ?? new BalanceAdjustmentJobOptions();

        string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

        builder.Services.AddQuartz(configure: q =>
        {
            q.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: "BalanceAdjustmentScheduler");

            q.AddJob<BalanceAdjustmentJob>(configure: j => j.WithIdentity(name: nameof(BalanceAdjustmentJob), group: jobOptions.Group));
            q.AddTrigger(configure: t => t
                .ForJob(jobName: nameof(BalanceAdjustmentJob), jobGroup: jobOptions.Group)
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
            .AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

        builder.Services.AddWorkerMetrics(workerName: "Worker.BalanceAdjustment");
        builder.Services.AddWorkerTracing(workerName: "Worker.BalanceAdjustment");

        WebApplication app = builder.Build();

        app.MapWorkerEndpoints();

        app.Run();
    }
}