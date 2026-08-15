using FinanceTracker.Worker.CurrencyRate.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.CircuitBreaker;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class ExchangeRateApiHealthCheckTests
{
	public sealed record Breaker(
		ResiliencePipeline Pipeline,
		CircuitBreakerStateProvider StateProvider,
		CircuitBreakerManualControl ManualControl
	);

	private static Breaker BuildBreaker()
	{
		CircuitBreakerStateProvider stateProvider = new CircuitBreakerStateProvider();
		CircuitBreakerManualControl manualControl = new CircuitBreakerManualControl();

		ResiliencePipeline pipeline = new ResiliencePipelineBuilder().AddCircuitBreaker(options: new CircuitBreakerStrategyOptions
		{
			FailureRatio = 1.0,
			MinimumThroughput = 2,
			SamplingDuration = TimeSpan.FromSeconds(value: 30),
			BreakDuration = TimeSpan.FromMinutes(value: 5),
			ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
			StateProvider = stateProvider,
			ManualControl = manualControl
		}).Build();

		return new Breaker(Pipeline: pipeline, StateProvider: stateProvider, ManualControl: manualControl);
	}

	private static async Task<HealthCheckResult> CheckAsync(
		CircuitBreakerStateProvider stateProvider
	) => await new ExchangeRateApiHealthCheck(stateProvider: stateProvider).CheckHealthAsync(
		context: new HealthCheckContext(),
		ct: CancellationToken.None
	);

	private static async Task DriveOpenAsync(Breaker breaker)
	{
		for (int attempt = 0; attempt < 4; attempt++)
		{
			try
			{
				await breaker.Pipeline.ExecuteAsync(callback: _ => throw new InvalidOperationException(message: "upstream is down"));
			}
			catch (InvalidOperationException)
			{
				// The failures are the point; the breaker counts them.
			}
			catch (BrokenCircuitException)
			{
				// Already open — nothing left to drive.
				return;
			}
		}
	}

	[Test]
	public async Task AReachableApiIsHealthy()
	{
		Breaker breaker = BuildBreaker();

		await breaker.Pipeline.ExecuteAsync(callback: _ => ValueTask.CompletedTask);

		HealthCheckResult result = await CheckAsync(stateProvider: breaker.StateProvider);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Healthy);
	}

	[Test]
	public async Task AnOpenCircuitIsUnhealthy()
	{
		Breaker breaker = BuildBreaker();

		await DriveOpenAsync(breaker: breaker);

		await Assert.That(value: breaker.StateProvider.CircuitState).IsEqualTo(expected: CircuitState.Open)
			.Because(message: "the test is meaningless unless the breaker actually opened");

		HealthCheckResult result = await CheckAsync(stateProvider: breaker.StateProvider);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy);
		await Assert.That(value: result.Description).IsNotNull();
	}

	[Test]
	public async Task AManuallyIsolatedCircuitIsUnhealthy()
	{
		Breaker breaker = BuildBreaker();

		await breaker.ManualControl.IsolateAsync();

		HealthCheckResult result = await CheckAsync(stateProvider: breaker.StateProvider);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy)
			.Because(message: "an operator cutting the upstream off deliberately still leaves the worker unable to do its job");
	}

	[Test]
	public async Task RecoveringFromIsolationReturnsToHealthy()
	{
		Breaker breaker = BuildBreaker();

		await breaker.ManualControl.IsolateAsync();
		await breaker.ManualControl.CloseAsync();

		HealthCheckResult result = await CheckAsync(stateProvider: breaker.StateProvider);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Healthy)
			.Because(message: "readiness has to come back on its own, or a resolved outage still needs a restart");
	}
}
