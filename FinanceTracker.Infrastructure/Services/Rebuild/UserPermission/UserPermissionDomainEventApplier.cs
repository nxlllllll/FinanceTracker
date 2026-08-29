using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.UserPermission;

namespace FinanceTracker.Infrastructure.Services.Rebuild.UserPermission;

/// <summary>
/// Applies user-permission domain events to the read model, for rebuilds.
/// </summary>
public sealed class UserPermissionDomainEventApplier(IUserPermissionWriteRepository repository)
{
	public Task ApplyAsync(
		IEvent @event,
		CancellationToken ct = default
	) => @event switch
	{
		UserPermissionCreated => Task.CompletedTask,
		PermissionGranted e => repository.GrantAsync(@event: e, ct: ct),
		PermissionRevoked e => repository.RevokeAsync(@event: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled user permission domain event: {@event.GetType().Name}", eventType: @event.GetType())
	};
}
