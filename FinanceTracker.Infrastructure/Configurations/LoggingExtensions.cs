using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Configurations;

public static class LoggingExtensions
{
	/// <summary>
	/// Replaces the default console provider with ZLogger.
	/// </summary>
	public static IHostApplicationBuilder AddStructuredLogging(this IHostApplicationBuilder builder)
	{
		builder.Logging.ClearProviders();

		if (builder.Environment.IsDevelopment())
		{
			builder.Logging.AddZLoggerConsole();
			return builder;
		}

		builder.Logging.AddZLoggerConsole(configure: options =>
		{
			options.IncludeScopes = true;
			options.UseJsonFormatter();
		});

		return builder;
	}
}
