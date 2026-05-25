using System.Text.Json.Serialization;

namespace FinanceTracker.Worker.CurrencyRate.Client;

public sealed record ExchangeRateApiResponse(
	[property: JsonPropertyName(name: "result")] string Result,
	[property: JsonPropertyName(name: "base_code")] string BaseCode,
	[property: JsonPropertyName(name: "conversion_rates")] Dictionary<string, decimal> ConversionRates
);
