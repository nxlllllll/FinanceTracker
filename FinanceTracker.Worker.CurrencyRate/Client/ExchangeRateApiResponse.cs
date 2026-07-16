using System.Text.Json.Serialization;

namespace FinanceTracker.Worker.CurrencyRate.Client;

/// <summary>
/// Deserialized response from the ExchangeRate-API v6 <c>/latest/{base_code}</c> endpoint.
/// </summary>
/// <param name="Result">API result status — <c>"success"</c> on a valid response.</param>
/// <param name="BaseCode">The base currency code for all rates in <paramref name="ConversionRates"/>.</param>
/// <param name="ConversionRates">
/// Dictionary of target currency codes to their exchange rates relative to <paramref name="BaseCode"/>.
/// </param>
public sealed record ExchangeRateApiResponse(
	[property: JsonPropertyName(name: "result")] string Result,
	[property: JsonPropertyName(name: "base_code")] string BaseCode,
	[property: JsonPropertyName(name: "conversion_rates")] Dictionary<string, decimal> ConversionRates
);
