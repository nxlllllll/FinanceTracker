using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.UserRole;

namespace FinanceTracker.Infrastructure.Services.Rebuild.UserRole;

/// <summary>
/// Applies user-role domain events to the read model, for rebuilds.
/// </summary>
public sealed class UserRoleDomainEventApplier(IUserRoleWriteRepository repository)
{
	public Task ApplyAsync(
		IEvent @event,
		CancellationToken ct = default
	) => @event switch
	{
		UserRoleCreated => Task.CompletedTask,
		RoleAssigned e => repository.AssignAsync(@event: e, ct: ct),
		RoleRemoved e => repository.RemoveAsync(@event: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled user role domain event: {@event.GetType().Name}", eventType: @event.GetType())
	};
}
