using FinanceTracker.Worker.Shared.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class QuartzHealthCheckTests
{
	private static async Task<HealthCheckResult> CheckAsync(bool isStarted, bool isShutdown)
	{
		IScheduler scheduler = Substitute.For<IScheduler>();
		scheduler.IsStarted.Returns(returnThis: isStarted);
		scheduler.IsShutdown.Returns(returnThis: isShutdown);

		ISchedulerFactory schedulerFactory = Substitute.For<ISchedulerFactory>();
		schedulerFactory.GetScheduler(cancellationToken: Arg.Any<CancellationToken>()).Returns(returnThis: scheduler);

		return await new QuartzHealthCheck(schedulerFactory: schedulerFactory).CheckHealthAsync(
			context: new HealthCheckContext(),
			ct: CancellationToken.None
		);
	}

	[Test]
	public async Task ARunningSchedulerIsHealthy()
	{
		HealthCheckResult result = await CheckAsync(isStarted: true, isShutdown: false);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Healthy);
	}

	[Test]
	public async Task ASchedulerThatNeverStartedIsUnhealthy()
	{
		HealthCheckResult result = await CheckAsync(isStarted: false, isShutdown: false);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy)
			.Because(message: "the host is up but nothing will ever fire, which is worse than being down because it looks fine");
	}

	[Test]
	public async Task AShutDownSchedulerIsUnhealthyEvenThoughItOnceStarted()
	{
		HealthCheckResult result = await CheckAsync(isStarted: true, isShutdown: true);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy);
	}

	[Test]
	public async Task AnUnhealthyResultSaysWhat()
	{
		HealthCheckResult result = await CheckAsync(isStarted: false, isShutdown: false);

		await Assert.That(value: result.Description).IsNotNull()
			.Because(message: "the description is what reaches the readiness payload and the on-call engineer reading it");
	}
}
