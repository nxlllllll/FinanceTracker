using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.CurrencyRate.Client;
using FinanceTracker.Worker.CurrencyRate.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.CurrencyRate;

public sealed class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

        builder.Services.AddInfrastructure(configuration: builder.Configuration);

        builder.Services
            .AddOptions<ExchangeRateApiOptions>()
            .BindConfiguration(configSectionPath: ExchangeRateApiOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHttpClient<ExchangeRateApiClient>();

        CurrencyRateJobOptions jobOptions = builder.Configuration
            .GetSection(key: CurrencyRateJobOptions.SectionName)
            .Get<CurrencyRateJobOptions>() ?? new CurrencyRateJobOptions();

        builder.Services.AddQuartz(configure: q =>
        {
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

        string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

        builder.Services.AddWorkerHealthChecks(connectionString: connectionString)
            .AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

        builder.Services.AddWorkerMetrics(workerName: "Worker.CurrencyRate");

        WebApplication app = builder.Build();

        app.MapWorkerEndpoints();

        app.Run();
    }
}