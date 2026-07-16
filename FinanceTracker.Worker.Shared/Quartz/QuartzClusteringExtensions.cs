using Quartz;

namespace FinanceTracker.Worker.Shared.Quartz;

/// <summary>
/// Configures a worker's Quartz scheduler to persist jobs/triggers to PostgreSQL and cluster across
/// multiple instances of the SAME worker, instead of relying on Quartz's default in-memory RAMJobStore.
/// </summary>
public static class QuartzClusteringExtensions
{
	public static void UseClusteredPostgresStore(
		this IServiceCollectionQuartzConfigurator quartz,
		string connectionString,
		string schedulerName)
	{
		quartz.SchedulerName = schedulerName;
		quartz.SchedulerId = "AUTO"; // unique per-instance ID, auto-derived per host/process

		quartz.UsePersistentStore(configure: store =>
		{
			store.UseProperties = true;
			store.UseClustering();
			store.UseSystemTextJsonSerializer();
			store.UsePostgres(configurer: postgresOptions => postgresOptions.ConnectionString = connectionString);
		});
	}
}
