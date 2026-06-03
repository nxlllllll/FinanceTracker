using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.Rebuild;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.Rebuild.Account;

public sealed class AccountProjectionRebuilder(
	IEventStore eventStore,
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
			logger.ZLogWarning(message: $"[Rebuild] No events found for Account {accountId}. Skipping.");
			return;
		}

		foreach (IEvent @event in result.Events)
			await applier.ApplyAsync(@event: @event, ct: ct);

		logger.ZLogInformation(message: $"[Rebuild] Completed rebuild for Account {accountId}. Applied {result.Events.Count} event(s).");
	}

	public async Task RebuildAllAsync(CancellationToken ct = default)
	{
		IReadOnlyList<Guid> ids = await eventStore.GetAggregateIdsAsync(
			aggregateType: AggregateTypeNames.Account,
			ct: ct
		);

		logger.ZLogInformation(message: $"[Rebuild] Starting full rebuild for {ids.Count} Account(s).");

		int rebuilt = 0;
		int failed = 0;

		foreach (Guid accountId in ids)
		{
			if (ct.IsCancellationRequested)
				break;

			try
			{
				await RebuildAsync(accountId: accountId, ct: ct);
				rebuilt++;
			}
			catch (Exception ex)
			{
				logger.ZLogError(exception: ex, message: $"[Rebuild] Failed to rebuild Account {accountId}. Error: {ex.Message}");
				failed++;
			}
		}

		logger.ZLogInformation(message: $"[Rebuild] Full rebuild complete. Rebuilt: {rebuilt}, failed: {failed}.");
	}
}