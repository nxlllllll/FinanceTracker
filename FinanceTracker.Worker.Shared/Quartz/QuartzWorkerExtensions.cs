using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Worker.Shared.HealthCheck;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FinanceTracker.Worker.Shared.Quartz;

public static class QuartzWorkerExtensions
{
	public static WebApplicationBuilder AddWorkerQuartz(
		this WebApplicationBuilder builder,
		Action<IServiceCollectionQuartzConfigurator> configureJobs)
	{
		string connectionString = builder.Configuration.RequireConnectionString(name: nameof(FinanceTrackerContext));
		string schedulerName = builder.Environment.ApplicationName;

		builder.Services.AddQuartz(configure: quartz =>
		{
			quartz.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: schedulerName);
			configureJobs(obj: quartz);
		});

		builder.Services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);
		builder.Services.AddHealthChecks().AddCheck<QuartzHealthCheck>(name: HealthCheckNames.Quartz, tags: [HealthCheckTags.Ready, HealthCheckTags.Scheduler]);

		return builder;
	}

	public static void AddCronJob<TJob>(
		this IServiceCollectionQuartzConfigurator quartz,
		string group,
		string triggerName,
		string cronExpression) where TJob : IJob
	{
		quartz.AddJob<TJob>(configure: job => job.WithIdentity(name: typeof(TJob).Name, group: group));
		quartz.AddTrigger(configure: trigger => trigger
			.ForJob(jobName: typeof(TJob).Name, jobGroup: group)
			.WithIdentity(name: triggerName, group: group)
			.WithCronSchedule(
				cronExpression: cronExpression,
				schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
			)
		);
	}

	public static void AddIntervalJob<TJob>(
		this IServiceCollectionQuartzConfigurator quartz,
		string group,
		string triggerName,
		TimeSpan interval) where TJob : IJob
	{
		quartz.AddJob<TJob>(configure: job => job.WithIdentity(name: typeof(TJob).Name, group: group));
		quartz.AddTrigger(configure: trigger => trigger
			.ForJob(jobName: typeof(TJob).Name, jobGroup: group)
			.WithIdentity(name: triggerName, group: group)
			.WithSimpleSchedule(action: schedule => schedule.WithInterval(timeSpan: interval).RepeatForever())
		);
	}
}
