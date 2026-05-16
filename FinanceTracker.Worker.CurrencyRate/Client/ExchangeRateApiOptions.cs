using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.CurrencyRate.Client;

public sealed class ExchangeRateApiOptions
{
	public const string SectionName = "ExchangeRateApi";

	[Required]
	public string ApiKey { get; init; } = null!;

	[Required]
	public string BaseUrl { get; init; } = "https://v6.exchangerate-api.com/v6";
}