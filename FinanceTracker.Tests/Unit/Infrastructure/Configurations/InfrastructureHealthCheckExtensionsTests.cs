using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinanceTracker.Tests.Unit.Infrastructure.Configurations;

public sealed class InfrastructureHealthCheckExtensionsTests
{
	private static readonly Func<HealthCheckRegistration, bool> LivePredicate = _ => false;
	private static readonly Func<HealthCheckRegistration, bool> ReadyPredicate = check => check.Tags.Contains(item: "ready");

	private static HealthCheckService BuildService(HealthStatus dependencyStatus)
	{
		ServiceCollection services = new ServiceCollection();
		services.AddLogging();

		services.AddHealthChecks()
			.AddCheck(name: "postgres", check: () => new HealthCheckResult(status: dependencyStatus), tags: ["ready", "db"])
			.AddCheck(name: "redis", check: () => new HealthCheckResult(status: dependencyStatus), tags: ["ready", "cache"]);

		return services.BuildServiceProvider().GetRequiredService<HealthCheckService>();
	}

	[Test]
	public async Task LivePredicate_ShouldStayHealthy_EvenWhenDependenciesAreDown()
	{
		HealthCheckService service = BuildService(dependencyStatus: HealthStatus.Unhealthy);

		HealthReport report = await service.CheckHealthAsync(predicate: LivePredicate);

		await Assert.That(value: report.Status).IsEqualTo(expected: HealthStatus.Healthy);
		await Assert.That(value: report.Entries).IsEmpty();
	}

	[Test]
	public async Task ReadyPredicate_ShouldReportUnhealthy_WhenADependencyIsDown()
	{
		HealthCheckService service = BuildService(dependencyStatus: HealthStatus.Unhealthy);

		HealthReport report = await service.CheckHealthAsync(predicate: ReadyPredicate);

		await Assert.That(value: report.Status).IsEqualTo(expected: HealthStatus.Unhealthy);
	}

	[Test]
	public async Task ReadyPredicate_ShouldReportHealthy_WhenAllDependenciesAreUp()
	{
		HealthCheckService service = BuildService(dependencyStatus: HealthStatus.Healthy);

		HealthReport report = await service.CheckHealthAsync(predicate: ReadyPredicate);

		await Assert.That(value: report.Status).IsEqualTo(expected: HealthStatus.Healthy);
		await Assert.That(value: report.Entries).ContainsKey(expectedKey: "postgres");
		await Assert.That(value: report.Entries).ContainsKey(expectedKey: "redis");
	}
}
