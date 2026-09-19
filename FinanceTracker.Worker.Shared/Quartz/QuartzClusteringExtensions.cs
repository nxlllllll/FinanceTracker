using Quartz;

namespace FinanceTracker.Worker.Shared.Quartz;

/// <summary>
/// Configures a worker's Quartz scheduler to persist jobs/triggers to PostgreSQL and cluster across
/// multiple instances of the SAME worker, instead of relying on Quartz's default in-memory RAMJobStore.
/// </summary>
public static class QuartzClusteringExtensions
{
	public static void UseClusteredPostgresStore(
		this IQuartzBuilder quartz,
		string connectionString,
		string schedulerName)
	{
		quartz.ConfigureScheduler(configure: scheduler =>
		{
			scheduler.InstanceName = schedulerName;
			scheduler.GenerateInstanceId = true; // unique per-instance ID, auto-derived per host/process
		});

		quartz.UsePersistentStore(configure: store =>
		{
			store.ConfigureStore(configure: options => options.StoreJobDataAsStrings = true);
			store.UseClustering();
			store.UseSystemTextJsonSerializer();
			store.UsePostgres(connectionString: connectionString);
		});
	}
}
