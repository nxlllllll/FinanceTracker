using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly.CircuitBreaker;

namespace FinanceTracker.Worker.CurrencyRate.HealthCheck;

/// <summary>
/// ASP.NET Core health check that reports the state of the ExchangeRate-API Polly circuit breaker.
/// Returns <c>Degraded</c> when half-open and <c>Unhealthy</c> when open or isolated.
/// </summary>
public sealed class ExchangeRateApiHealthCheck(CircuitBreakerStateProvider stateProvider) : IHealthCheck
{
	public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) => stateProvider.CircuitState switch
	{
		CircuitState.Closed => HealthCheckResult.Healthy(description: "ExchangeRateApi is reachable."),
		CircuitState.HalfOpen => HealthCheckResult.Degraded(description: "ExchangeRateApi circuit is half-open — probing."),
		CircuitState.Open => HealthCheckResult.Unhealthy(description: "ExchangeRateApi circuit is OPEN — service unavailable."),
		CircuitState.Isolated => HealthCheckResult.Unhealthy(description: "ExchangeRateApi circuit is isolated manually."),
		_ => HealthCheckResult.Healthy()
	};
}