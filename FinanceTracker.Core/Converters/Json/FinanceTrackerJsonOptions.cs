using System.Text.Json;

namespace FinanceTracker.Core.Converters.Json;

public static class FinanceTrackerJsonOptions
{
	public static readonly JsonSerializerOptions Payload = new JsonSerializerOptions
	{
		PropertyNamingPolicy = null,
		WriteIndented = false,
		Converters = { new UtcDateTimeConverter() }
	};
}