using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Core.Services.Rebuild;

namespace FinanceTracker.Infrastructure.Services.Rebuild.UserPermission;

[Projection(name: "permission", aggregateType: AggregateTypeNames.UserPermission)]
public sealed class UserPermissionProjectionRebuild(
	IUserPermissionWriteRepository repository,
	UserPermissionDomainEventApplier applier
) : IProjectionRebuild
{
	public Task ClearAsync(Guid aggregateId, CancellationToken ct = default)
		=> repository.DeleteAllForUserAsync(userId: aggregateId, ct: ct);

	public Task ApplyAsync(IEvent @event, CancellationToken ct = default)
		=> applier.ApplyAsync(@event: @event, ct: ct);
}
