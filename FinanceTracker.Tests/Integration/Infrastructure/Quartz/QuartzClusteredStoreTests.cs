using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.Shared.Quartz;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Quartz;

namespace FinanceTracker.Tests.Integration.Infrastructure.Quartz;

public sealed class QuartzClusteredStoreTests : DatabaseFixture
{
	private static readonly TimeSpan FireTimeout = TimeSpan.FromSeconds(value: 30);

	private readonly QuartzProbeSignal _signal = new QuartzProbeSignal();
	private WebApplication _worker = null!;

	[Before(hookType: Test)]
	public async Task StartWorkerAsync()
	{
		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();

		builder.WebHost.UseUrls(urls: "http://127.0.0.1:0");
		builder.Configuration[$"ConnectionStrings:{nameof(FinanceTrackerContext)}"] = Context.Database.GetConnectionString();

		builder.Services.AddSingleton(implementationInstance: _signal);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddIntervalJob<QuartzProbeJob>(
			group: nameof(QuartzClusteredStoreTests),
			triggerName: nameof(QuartzProbeJob),
			interval: TimeSpan.FromHours(value: 1)
		));

		_worker = builder.Build();

		await _worker.StartAsync();
	}

	[After(hookType: Test)]
	public async Task StopWorkerAsync()
	{
		await _worker.StopAsync();
		await _worker.DisposeAsync();
	}

	private async Task<long> CountAsync(string sql)
	{
		await using NpgsqlConnection connection = new NpgsqlConnection(connectionString: Context.Database.GetConnectionString());
		await connection.OpenAsync();

		await using NpgsqlCommand command = new NpgsqlCommand(cmdText: sql, connection: connection);
		command.Parameters.AddWithValue(parameterName: "job", value: nameof(QuartzProbeJob));
		command.Parameters.AddWithValue(parameterName: "unclustered", value: QuartzSchedulerOptions.DefaultInstanceId);

		return (long)(await command.ExecuteScalarAsync())!;
	}

	[Test]
	public async Task AJobScheduledThroughTheWorkerSetupFiresOnTheMigratedSchema()
	{
		Task fired = _signal.Fired.Task;

		await Assert.That(action: async () => await fired.WaitAsync(timeout: FireTimeout)).ThrowsNothing().Because(message: """
			This is the only place the scheduler runs against the real schema. Quartz 4 checks its tables
			when it starts and refuses to acquire triggers on a schema it does not recognise, so a migration
			that missed a column shows up here as a job that never fires.
		""");
	}

	[Test]
	public async Task TheJobIsKeptInPostgresRatherThanInMemory()
	{
		await _signal.Fired.Task.WaitAsync(timeout: FireTimeout);

		long stored = await CountAsync(sql: "SELECT COUNT(*) FROM qrtz_job_details WHERE job_name = @job");

		await Assert.That(value: stored).IsEqualTo(expected: 1).Because(message: """
			The worker is meant to share its schedule with every other instance of itself. A job that runs
			but is not in the table ran from an in-memory store, and each instance would fire it on its own.
		""");
	}

	[Test]
	public async Task TheSchedulerJoinsTheClusterUnderAGeneratedInstanceId()
	{
		await _signal.Fired.Task.WaitAsync(timeout: FireTimeout);

		long clustered = await CountAsync(sql: "SELECT COUNT(*) FROM qrtz_scheduler_state WHERE instance_name <> @unclustered");

		await Assert.That(value: clustered).IsEqualTo(expected: 1).Because(message: """
			Two instances checking in under the same fixed id look like one node to the cluster, so neither
			notices when the other dies and its triggers are never recovered.
		""");
	}

	[Test]
	public async Task TheQuartzHealthCheckReportsARunningSchedulerAsHealthy()
	{
		await _signal.Fired.Task.WaitAsync(timeout: FireTimeout);

		HealthReport report = await _worker.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(
			predicate: registration => registration.Name == HealthCheckNames.Quartz
		);

		await Assert.That(value: report.Entries[HealthCheckNames.Quartz].Status).IsEqualTo(expected: HealthStatus.Healthy);
	}
}
