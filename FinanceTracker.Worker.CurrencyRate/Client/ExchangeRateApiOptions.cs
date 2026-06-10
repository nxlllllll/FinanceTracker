using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.CurrencyRate.Client;

/// <summary>
/// Configuration for the ExchangeRate-API HTTP client and its Polly resilience policies.
/// Bind from <c>appsettings.json</c> under the <c>"ExchangeRateApi"</c> section.
/// </summary>
public sealed class ExchangeRateApiOptions
{
	public const string SectionName = "ExchangeRateApi";

	/// <summary>When <c>false</c>, all API calls are skipped — useful for local development without an API key.</summary>
	public bool IsEnabled { get; init; } = true;

	/// <summary>ExchangeRate-API v6 authentication key.</summary>
	[Required]
	public string ApiKey { get; init; } = null!;

	/// <summary>Base URL of the ExchangeRate-API v6 endpoint. Default: <c>https://v6.exchangerate-api.com/v6</c>.</summary>
	[Required]
	public string BaseUrl { get; init; } = "https://v6.exchangerate-api.com/v6";

	/// <summary>Number of Polly retry attempts on transient HTTP failures. Default: 3.</summary>
	public int RetryCount { get; init; } = 3;

	/// <summary>Delay in seconds between retry attempts. Default: 2.</summary>
	public int RetryDelaySeconds { get; init; } = 2;

	/// <summary>Failure ratio threshold that trips the circuit breaker (0.0–1.0). Default: 0.5.</summary>
	public double CircuitBreakerFailureRatio { get; init; } = 0.5;

	/// <summary>Minimum number of requests before circuit breaker evaluates the failure ratio. Default: 3.</summary>
	public int CircuitBreakerMinThroughput { get; init; } = 3;

	/// <summary>Sliding window duration in seconds for circuit breaker failure counting. Default: 30.</summary>
	public int CircuitBreakerSamplingSeconds { get; init; } = 30;

	/// <summary>Duration in seconds the circuit stays open before transitioning to half-open. Default: 30.</summary>
	public int CircuitBreakerBreakSeconds { get; init; } = 30;

	/// <summary>HTTP request timeout in seconds. Default: 10.</summary>
	public int TimeoutSeconds { get; init; } = 10;
}