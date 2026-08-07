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

		builder.Logging.Configure(action: options =>
		{
			options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId;
		});

		builder.Logging.AddZLoggerConsole(configure: options =>
		{
			options.IncludeScopes = true;
			options.UseJsonFormatter(configure: formatter =>
			{
				formatter.IncludeProperties = IncludeProperties.Timestamp |
					IncludeProperties.LogLevel |
					IncludeProperties.CategoryName |
					IncludeProperties.Message |
					IncludeProperties.Exception |
					IncludeProperties.ScopeKeyValues |
					IncludeProperties.ParameterKeyValues;

				formatter.KeyNameMutator = KeyNameMutator.LastMemberNameLowerFirstCharacter;
			});
		});

		return builder;
	}
}
