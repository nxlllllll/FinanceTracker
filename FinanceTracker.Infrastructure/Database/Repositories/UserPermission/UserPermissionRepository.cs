using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserPermission;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserPermission;

public sealed class UserPermissionRepository(
	IEventStore eventStore,
	IUnitOfWork unitOfWork
) : IUserPermissionRepository
{
	private const string AggregateType = AggregateTypeNames.UserPermission;

	public async Task<Core.Domains.UserPermission.UserPermission?> GetByUserIdAsync(
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

		return Core.Domains.UserPermission.UserPermission.ReconstituteFromHistory(history: result.Events);
	}

	public async Task SaveAsync(
		Core.Domains.UserPermission.UserPermission userPermission,
		CancellationToken ct = default)
	{
		if (userPermission.Events.Count == 0)
			return;

		await eventStore.SaveAsync(
			aggregateId: userPermission.Id,
			aggregateType: AggregateType,
			events: userPermission.Events,
			expectedVersion: userPermission.PersistedVersion,
			ct: ct
		);

		unitOfWork.OnCommitted(callback: userPermission.ClearEvents);
	}
}
