using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.UserPermission;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.UserPermission;

namespace FinanceTracker.Worker.PermissionProjection.Projection;

/// <summary>
/// Applies individual permission integration events to the read model projection.
/// <see cref="UserPermissionCreatedEvent"/> is a genuine no-op here — unlike Account, there is no
/// "header" row per user; a user with zero grants simply has zero rows in <c>user_permissions</c>.
/// Used exclusively by <see cref="PermissionProjection"/>.
/// </summary>
public sealed class PermissionEventApplier(IUserPermissionWriteRepository repository)
{
	public Task ApplyAsync(IIntegrationEvent @event, CancellationToken ct = default) => @event switch
	{
		UserPermissionCreatedEvent => Task.CompletedTask,
		PermissionGrantedEvent e => ApplyAsync(e: e, ct: ct),
		PermissionRevokedEvent e => ApplyAsync(e: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled integration event: {@event.GetType().Name}", eventType: @event.GetType())
	};

	private Task ApplyAsync(PermissionGrantedEvent e, CancellationToken ct)
	{
		return repository.GrantAsync(
			@event: new PermissionGranted(
				Id: e.EventId,
				UserId: e.UserId,
				GrantedBy: e.GrantedBy,
				Permission: e.Permission,
				Version: e.Version,
				OccurredAt: e.OccurredAt
			),
			ct: ct
		);
	}

	private Task ApplyAsync(PermissionRevokedEvent e, CancellationToken ct)
	{
		return repository.RevokeAsync(
			userId: e.UserId,
			permission: e.Permission,
			ct: ct
		);
	}
}

