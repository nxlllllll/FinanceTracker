using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceTracker.Core.Converters.Json;

public static class FinanceTrackerJsonOptions
{
	public static readonly JsonSerializerOptions Payload = new JsonSerializerOptions
	{
		PropertyNamingPolicy = null,
		WriteIndented = false,
		Converters = { new JsonStringEnumConverter() }
	};

	public static readonly JsonSerializerOptions Application = BuildApplication();

	private static JsonSerializerOptions BuildApplication()
	{
		JsonSerializerOptions opts = new JsonSerializerOptions(options: Payload);
		opts.Converters.Add(item: new ResultJsonConverterFactory());
		return opts;
	}
}
