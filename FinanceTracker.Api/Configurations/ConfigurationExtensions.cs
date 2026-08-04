using FinanceTracker.Core.Exceptions.ConfigurationExceptions;

namespace FinanceTracker.Api.Configurations;

public static class ConfigurationExtensions
{
	public static string RequireValue(this IConfiguration configuration, string path)
	{
		string? value = configuration[key: path];

		if (String.IsNullOrWhiteSpace(value: value))
			throw new ConfigurationException(message: $"Configuration value '{path}' is required but was not provided.");

		return value;
	}
}
