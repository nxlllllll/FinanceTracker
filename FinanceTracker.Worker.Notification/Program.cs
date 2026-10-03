using FinanceTracker.Contracts.Messages;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Notification.Consumer;
using FinanceTracker.Worker.Notification.Mapping;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.Notification;

public sealed class Program
{
	public const string TemplatesFileName = "notification-templates.json";

	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.Configuration.AddJsonFile(
			path: TemplatesFileName,
			optional: false,
			reloadOnChange: false
		);
		builder.AddWorkerDefaults();

		builder.Services.AddNotifications();

		builder.Services.Scan(action: scan => scan
			.FromAssemblyOf<Program>()
			.AddClasses(action: classes => classes.AssignableTo<IUserNotificationMapper>())
			.As<IUserNotificationMapper>()
			.WithScopedLifetime()
		);

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, UserNotificationConsumer>()
			.AddRabbitMqHealthCheck();

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
