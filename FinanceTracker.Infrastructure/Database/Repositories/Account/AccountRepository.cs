using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Infrastructure.Database.Repositories.Account;

public sealed class AccountRepository(
	IAccountReadRepository accountReadRepository,
	IEventStore eventStore
) : IAccountRepository
{
	private const string AggregateType = nameof(Core.Domains.Account.Account);

	public async Task<Core.Domains.Account.Account?> GetByIdAsync(
		Guid accountId,
		CancellationToken ct = default)
	{
		AccountDto? dto = await accountReadRepository.GetByIdAsync(accountId, ct);
		if (dto is null) 
			return null;

		EventStoreResult result = await eventStore.LoadAsync(accountId, ct);
		if (result.Events.Count == 0 && result.Snapshot is null)
			return null;
		
		return Core.Domains.Account.Account.Reconstitute(
			metadata: dto,
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
			ct: ct
		);

		account.ClearEvents();
	}
}