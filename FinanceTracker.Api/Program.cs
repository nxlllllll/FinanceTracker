using FinanceTracker.Application.Configurations;
using FinanceTracker.Infrastructure.Configurations;

namespace FinanceTracker.Api;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Host.UseDefaultServiceProvider(configure: options =>
		{
			options.ValidateScopes = true;
			options.ValidateOnBuild = true;
		});

		builder.Services.AddApplication();
		builder.Services.AddPersistence(configuration: builder.Configuration);
		builder.Services.AddAuth();

		builder.Services.AddHealthChecks();

		WebApplication app = builder.Build();

		app.MapHealthChecks(pattern: "/health/live");

		app.Run();
	}
}

