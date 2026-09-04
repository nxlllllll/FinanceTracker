using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.Rebuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.Rebuild;

/// <summary>
/// Replays the event log back into a projection, for one aggregate or for every aggregate of its type.
/// </summary>
public sealed class ProjectionRebuilder(
	IEventStore eventStore,
	IUnitOfWork unitOfWork,
	IServiceScopeFactory scopeFactory,
	ILogger<ProjectionRebuilder> logger)
{
	/// <summary>Rebuilds one aggregate, inside a transaction.</summary>
	public async Task RebuildAsync(
		IProjectionRebuild projection,
		string aggregateType,
		Guid aggregateId,
		CancellationToken ct = default)
	{
		logger.ZLogInformation(message: $"[Rebuild] Starting {aggregateType} {aggregateId}.");

		IReadOnlyList<IEvent> events = await eventStore.LoadAllEventsAsync(
			aggregateId: aggregateId,
			aggregateType: aggregateType,
			ct: ct
		);

		if (events.Count == 0)
		{
			logger.ZLogWarning(message: $"[Rebuild] {aggregateType} {aggregateId} has no events in the log. Leaving the projection untouched — an empty log is not evidence that the projection should be empty.");
			return;
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await projection.ClearAsync(aggregateId: aggregateId, ct: ct);

			foreach (IEvent @event in events)
				await projection.ApplyAsync(@event: @event, ct: ct);
		}, ct: ct);

		logger.ZLogInformation(message: $"[Rebuild] Completed {aggregateType} {aggregateId}. Applied {events.Count} event(s).");
	}

	/// <summary>
	/// Rebuilds every aggregate of the type, in batches.
	/// </summary>
	public async Task RebuildAllAsync(
		string projectionName,
		int batchSize,
		int parallelism,
		CancellationToken ct = default)
	{
		string aggregateType = ResolveAggregateType(projectionName: projectionName);

		logger.ZLogInformation(message: $"[Rebuild] Starting every {aggregateType}, {batchSize} per batch, {parallelism} at a time.");

		List<Guid> batch = new List<Guid>(capacity: batchSize);
		int succeeded = 0;
		int failed = 0;

		await foreach (Guid aggregateId in eventStore.GetAggregateIdsAsync(aggregateType: aggregateType, ct: ct))
		{
			batch.Add(item: aggregateId);

			if (batch.Count < batchSize)
				continue;

			(int batchSucceeded, int batchFailed) = await ProcessBatchAsync(
				projectionName: projectionName,
				aggregateType: aggregateType,
				aggregateIds: batch,
				parallelism: parallelism,
				ct: ct
			);

			succeeded += batchSucceeded;
			failed += batchFailed;
			batch.Clear();

			logger.ZLogInformation(message: $"[Rebuild] {succeeded + failed} {aggregateType}(s) processed so far ({failed} failed).");
		}

		if (batch.Count > 0)
		{
			(int lastSucceeded, int lastFailed) = await ProcessBatchAsync(
				projectionName: projectionName,
				aggregateType: aggregateType,
				aggregateIds: batch,
				parallelism: parallelism,
				ct: ct
			);

			succeeded += lastSucceeded;
			failed += lastFailed;
		}

		logger.ZLogInformation(message: $"[Rebuild] Finished every {aggregateType}: {succeeded} rebuilt, {failed} failed.");
	}

	private async Task<(int Succeeded, int Failed)> ProcessBatchAsync(
		string projectionName,
		string aggregateType,
		IReadOnlyList<Guid> aggregateIds,
		int parallelism,
		CancellationToken ct)
	{
		int succeeded = 0;
		int failed = 0;

		await Parallel.ForEachAsync(
			source: aggregateIds,
			parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = parallelism, CancellationToken = ct },
			body: async (aggregateId, token) =>
			{
				await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

				try
				{
					ProjectionRegistry registry = scope.ServiceProvider.GetRequiredService<ProjectionRegistry>();
					ProjectionRebuilder rebuilder = scope.ServiceProvider.GetRequiredService<ProjectionRebuilder>();

					(IProjectionRebuild Projection, string AggregateType)? resolved = registry.Resolve(name: projectionName);

					await rebuilder.RebuildAsync(
						projection: resolved!.Value.Projection,
						aggregateType: aggregateType,
						aggregateId: aggregateId,
						ct: token
					);

					Interlocked.Increment(location: ref succeeded);
				}
				catch (Exception ex)
				{
					Interlocked.Increment(location: ref failed);
					logger.ZLogError(exception: ex, message: $"[Rebuild] {aggregateType} {aggregateId} failed. Continuing with the rest.");
				}
			}
		);

		return (succeeded, failed);
	}

	private static string ResolveAggregateType(string projectionName)
		=> ProjectionRegistry.AggregateTypeOfName(name: projectionName)
		   ?? throw new ArgumentException(message: $"No projection named '{projectionName}'.", paramName: nameof(projectionName));
}
