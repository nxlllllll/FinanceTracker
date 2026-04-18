using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories;

namespace FinanceTracker.Infrastructure.Database.Repositories;

public sealed class AccountRepository(
	IEventStore eventStore
) : IAccountRepository
{
	private readonly string _aggregateType = nameof(Account);
	
	public async Task<Account?> GetByIdAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		IReadOnlyList<IEvent> events = await eventStore.LoadAsync(aggregateId: accountId, ct: ct);
		if (events.Count == 0)
			return null;
		
		return Account.ReconstituteFromHistory(history: events);
	}

	public async Task SaveAsync(
		Account account,
		CancellationToken ct = default)
	{
		if (account.Events.Count == 0)
			return;
		
		int expectedVersion = account.Version - account.Events.Count;

		await eventStore.SaveAsync(
			aggregateId: account.Id,
			aggregateType: _aggregateType,
			events: account.Events,
			expectedVersion: expectedVersion,
			ct: ct
		);
		
		account.ClearEvents();
	}
}