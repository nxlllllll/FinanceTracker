using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountRepository(
	IEventStore eventStore
) : IAccountRepository
{
	private const string AggregateType = nameof(Core.Domains.Account.Account);

	public async Task<Core.Domains.Account.Account?> GetByIdAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		EventStoreResult result = await eventStore.LoadAsync(aggregateId: accountId, ct: ct);
		if (result.Events.Count == 0 && result.Snapshot is null)
			return null;

		if (result.Snapshot is null)
			return Core.Domains.Account.Account.ReconstituteFromHistory(history: result.Events);
		
		Core.Domains.Account.Account account = Core.Domains.Account.Account.Restore(snapshot: result.Snapshot);
		account.LoadEventsFromHistory(history: result.Events);
		return account;

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
			ct: ct
		);

		account.ClearEvents();
	}
}