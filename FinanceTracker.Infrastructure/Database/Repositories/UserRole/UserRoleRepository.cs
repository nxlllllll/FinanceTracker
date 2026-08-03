using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserRole;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserRole;

public sealed class UserRoleRepository(
	IEventStore eventStore,
	IUnitOfWork unitOfWork
) : IUserRoleRepository
{
	private const string AggregateType = AggregateTypeNames.UserRole;

	public async Task<Core.Domains.UserRole.UserRole?> GetByUserIdAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		EventStoreResult result = await eventStore.LoadAsync(
			aggregateId: userId,
			aggregateType: AggregateType,
			ct: ct
		);

		if (result.Events.Count == 0 && result.Snapshot is null)
			return null;

		return Core.Domains.UserRole.UserRole.ReconstituteFromHistory(history: result.Events);
	}

	public async Task SaveAsync(
		Core.Domains.UserRole.UserRole userRole,
		CancellationToken ct = default)
	{
		if (userRole.Events.Count == 0)
			return;

		await eventStore.SaveAsync(
			aggregateId: userRole.Id,
			aggregateType: AggregateType,
			events: userRole.Events,
			expectedVersion: userRole.PersistedVersion,
			ct: ct
		);

		unitOfWork.OnCommitted(callback: userRole.ClearEvents);
	}
}
