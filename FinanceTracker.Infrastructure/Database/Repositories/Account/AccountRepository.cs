using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountRepository(
	IEventStore eventStore
) : IAccountRepository
{
	private const string AggregateType = AggregateTypeNames.Account;

	public async Task<Core.Domains.Account.Account?> GetByIdAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		EventStoreResult result = await eventStore.LoadAsync(
			aggregateId: accountId,
			aggregateType: AggregateType,
			ct: ct
		);
		if (result.Events.Count == 0 && result.Snapshot is null)
			return null;
		
		return Core.Domains.Account.Account.Reconstitute(
			snapshot: result.Snapshot,
			events: result.Events
		);
	}

	public async Task SaveAsync(
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		if (account.Events.Count == 0)
			return;	

		int expectedVersion = account.Version - account.Events.Count;

		await eventStore.SaveAsync(
			aggregateId: account.Id,
			aggregateType: AggregateType,
			events: account.Events,
			expectedVersion: expectedVersion,
			snapshotFactory: account.TakeSnapshot,
			ct: ct
		);

		account.ClearEvents();
	}
}
