using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Core.Services.Rebuild;

namespace FinanceTracker.Infrastructure.Services.Rebuild.UserRole;

[Projection(name: "user-role", aggregateType: AggregateTypeNames.UserRole)]
public sealed class UserRoleProjectionRebuild(
	IUserRoleWriteRepository repository,
	UserRoleDomainEventApplier applier
) : IProjectionRebuild
{
	public Task ClearAsync(Guid aggregateId, CancellationToken ct = default)
		=> repository.DeleteAllForUserAsync(userId: aggregateId, ct: ct);

	public Task ApplyAsync(IEvent @event, CancellationToken ct = default)
		=> applier.ApplyAsync(@event: @event, ct: ct);
}
