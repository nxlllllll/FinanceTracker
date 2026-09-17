using System.Reflection;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinanceTracker.Tests.Integration.Infrastructure.RabbitMQ;

public sealed class RabbitMqHealthCheckTests : RabbitMqFixture
{
	private RabbitMqHealthCheck _healthCheck = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		Uri uri = new Uri(uriString: ConnectionString);

		RabbitMqOptions options = new RabbitMqOptions
		{
			Host = uri.Host,
			Port = uri.Port,
			Username = "guest",
			Password = "guest"
		};

		_healthCheck = new RabbitMqHealthCheck(connectionFactory: new RabbitMqConnectionFactory(options: Options.Create(options: options)));
	}

	[After(hookType: Test)]
	public async Task TeardownAsync()
	{
		await _healthCheck.DisposeAsync();
	}

	private static IConnection? GetConnection(RabbitMqHealthCheck healthCheck)
	{
		FieldInfo? field = typeof(RabbitMqHealthCheck).GetField(
			name: "_connection",
			bindingAttr: BindingFlags.NonPublic | BindingFlags.Instance
		);

		return field?.GetValue(obj: healthCheck) as IConnection;
	}

	private Task<HealthCheckResult> CheckAsync()
		=> _healthCheck.CheckHealthAsync(context: new HealthCheckContext());

	[Test]
	public async Task CheckHealthAsync_WhenTheBrokerIsReachable_ShouldReportHealthy()
	{
		HealthCheckResult result = await CheckAsync();

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Healthy);
	}

	[Test]
	public async Task CheckHealthAsync_CalledRepeatedly_ShouldKeepOneConnection()
	{
		await CheckAsync();
		IConnection? first = GetConnection(healthCheck: _healthCheck);

		await CheckAsync();
		IConnection? second = GetConnection(healthCheck: _healthCheck);

		await Assert.That(value: first).IsNotNull();
		await Assert.That(value: ReferenceEquals(objA: first, objB: second)).IsTrue().Because(message: """
			Prometheus runs every health check on each scrape. Opening a fresh connection per run means a
			TCP handshake, authentication and two broker log lines every fifteen seconds for each worker,
			all to learn that nothing changed.
		""");
	}

	[Test]
	public async Task CheckHealthAsync_WhenTheConnectionWasClosed_ShouldReconnect()
	{
		await CheckAsync();
		IConnection? closed = GetConnection(healthCheck: _healthCheck);

		await Assert.That(value: closed).IsNotNull();
		await closed!.CloseAsync();

		HealthCheckResult result = await CheckAsync();
		IConnection? reopened = GetConnection(healthCheck: _healthCheck);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Healthy);
		await Assert.That(value: reopened).IsNotNull();
		await Assert.That(value: reopened!.IsOpen).IsTrue().Because(message: """
			A broker restart closes the kept connection. If the check went on reporting that closed
			connection, the worker would stay unready after RabbitMQ is back, or stay ready while it is gone.
		""");
	}
}
