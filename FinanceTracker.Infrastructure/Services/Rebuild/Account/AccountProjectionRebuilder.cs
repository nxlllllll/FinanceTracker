using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.Rebuild;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.Rebuild.Account;

public sealed class AccountProjectionRebuilder(
	IEventStore eventStore,
	IAccountWriteRepository writeRepository,
	ISnapshotSerializer<Core.Domains.Account.Account> snapshotSerializer,
	IUnitOfWork unitOfWork,
	AccountDomainEventApplier applier,
	ILogger<AccountProjectionRebuilder> logger
) : IAccountProjectionRebuilder
{
	public async Task RebuildAsync(Guid accountId, CancellationToken ct = default)
	{
		logger.ZLogInformation(message: $"[Rebuild] Starting rebuild for Account {accountId}.");

		Core.Domains.Abstractions.EventStore.EventStoreResult result = await eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateTypeNames.Account,
			ct: ct
		);

		if (result.Events.Count == 0 && result.Snapshot is null)
		{
			logger.ZLogWarning(message: $"[Rebuild] No events or snapshot found for Account {accountId}. Skipping.");
			return;
		}

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			if (result.Snapshot is not null)
			{
				Core.Domains.Account.Account accountFromSnapshot = snapshotSerializer.Deserialize(snapshot: result.Snapshot);
				await writeRepository.UpsertFromSnapshotAsync(account: accountFromSnapshot, ct: ct);

				logger.ZLogInformation(message: $"[Rebuild] Applied snapshot at version {result.Snapshot.Version} for Account {accountId}.");
			}
			else await writeRepository.DeleteAsync(accountId: accountId, ct: ct);

			foreach (IEvent @event in result.Events)
				await applier.ApplyAsync(@event: @event, ct: ct);

			logger.ZLogInformation(message: $"[Rebuild] Completed rebuild for Account {accountId}. Applied {result.Events.Count} event(s) after snapshot.");
		}, ct: ct);
	}

	public async Task RebuildAllAsync(int batchSize = 50, CancellationToken ct = default)
	{
		logger.ZLogInformation(message: $"[Rebuild] Starting full rebuild (batchSize: {batchSize}).");

		List<Guid> batch = new List<Guid>(capacity: batchSize);
		int rebuilt = 0;
		int failed = 0;

		await foreach (Guid accountId in eventStore.GetAggregateIdsAsync(aggregateType: AggregateTypeNames.Account, ct: ct))
		{
			if (ct.IsCancellationRequested)
				break;

			batch.Add(item: accountId);

			if (batch.Count < batchSize)
				continue;

			(int batchRebuilt, int batchFailed) = await ProcessBatchAsync(batch: batch, ct: ct);

			rebuilt += batchRebuilt;
			failed += batchFailed;
			batch.Clear();
		}

		if (batch.Count > 0 && !ct.IsCancellationRequested)
		{
			(int batchRebuilt, int batchFailed) = await ProcessBatchAsync(batch: batch, ct: ct);

			rebuilt += batchRebuilt;
			failed += batchFailed;
		}

		logger.ZLogInformation(message: $"[Rebuild] Full rebuild complete. Rebuilt: {rebuilt}, failed: {failed}.");
	}

	private async Task<(int Rebuilt, int Failed)> ProcessBatchAsync(
		List<Guid> batch,
		CancellationToken ct)
	{
		logger.ZLogInformation(message: $"[Rebuild] Processing batch of {batch.Count} account(s).");

		int rebuilt = 0;
		int failed = 0;

		SemaphoreSlim semaphore = new SemaphoreSlim(initialCount: 5, maxCount: 5);
		IEnumerable<Task> tasks = batch.Select(selector: async accountId =>
		{
			await semaphore.WaitAsync(cancellationToken: ct);
			try
			{
				await RebuildAsync(accountId: accountId, ct: ct);
				Interlocked.Increment(location: ref rebuilt);
			}
			catch (Exception ex)
			{
				logger.ZLogError(exception: ex, message: $"[Rebuild] Failed to rebuild Account {accountId}: {ex.Message}");
				Interlocked.Increment(location: ref failed);
			}
			finally
			{
				semaphore.Release();
			}
		});

		await Task.WhenAll(tasks: tasks);

		return (rebuilt, failed);
	}
}