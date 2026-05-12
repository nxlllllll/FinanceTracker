using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceTracker.Core.Converters.Json;

public static class FinanceTrackerJsonOptions
{
	public static readonly JsonSerializerOptions Payload = new JsonSerializerOptions
	{
		PropertyNamingPolicy = null,
		WriteIndented = false,
		Converters =
		{
			new UtcDateTimeConverter(),
			new JsonStringEnumConverter()
		}
	};
}