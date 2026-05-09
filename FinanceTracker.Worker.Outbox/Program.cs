using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Worker.Outbox.Jobs;
using FinanceTracker.Worker.Outbox.RabbitMQ;
using Quartz;

namespace FinanceTracker.Worker.Outbox;

public sealed class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

        builder.Services.AddInfrastructure(configuration: builder.Configuration);

        builder.Services.AddOptions<RabbitMqOptions>()
            .BindConfiguration(configSectionPath: RabbitMqOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<RabbitMqConnectionFactory>();

        OutboxOptions outboxOptions = builder.Configuration
            .GetSection(key: OutboxOptions.SectionName)
            .Get<OutboxOptions>() ?? new OutboxOptions();

        builder.Services.AddQuartz(configure: q =>
        {
            q.AddJob<OutboxPublisherJob>(configure: j => j.WithIdentity(name: nameof(OutboxPublisherJob), group: outboxOptions.Group));

            q.AddTrigger(configure: t => t
                .ForJob(jobName: nameof(OutboxPublisherJob), jobGroup: outboxOptions.Group)
                .WithIdentity(name: outboxOptions.TriggerName, group: outboxOptions.Group)
                .WithSimpleSchedule(action: s => s.WithIntervalInSeconds(seconds: outboxOptions.IntervalSeconds).RepeatForever())
            );
        });

        builder.Services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);

        IHost app = builder.Build();
        app.Run();
    }
}