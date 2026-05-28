using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly.CircuitBreaker;

namespace FinanceTracker.Worker.CurrencyRate.HealthCheck;

public sealed class ExchangeRateApiHealthCheck(
	CircuitBreakerStateProvider stateProvider
) : IHealthCheck
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