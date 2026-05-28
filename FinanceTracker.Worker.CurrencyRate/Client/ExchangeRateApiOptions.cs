using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.CurrencyRate.Client;

public sealed class ExchangeRateApiOptions
{
	public const string SectionName = "ExchangeRateApi";

	public bool IsEnabled { get; init; } = true;

	[Required]
	public string ApiKey { get; init; } = null!;

	[Required]
	public string BaseUrl { get; init; } = "https://v6.exchangerate-api.com/v6";

	public int RetryCount { get; init; } = 3;
	public int RetryDelaySeconds { get; init; } = 2;
	public double CircuitBreakerFailureRatio { get; init; } = 0.5;
	public int CircuitBreakerMinThroughput { get; init; } = 3;
	public int CircuitBreakerSamplingSeconds { get; init; } = 30;
	public int CircuitBreakerBreakSeconds { get; init; } = 30;
	public int TimeoutSeconds { get; init; } = 10;
}