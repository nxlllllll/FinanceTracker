using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.UserPermission;
using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Cache;

namespace FinanceTracker.Worker.PermissionProjection.Projection;

/// <summary>
/// Applies individual permission integration events to the read model projection.
/// <see cref="UserPermissionCreatedEvent"/> is a genuine no-op here — unlike Account, there is no
/// "header" row per user; a user with zero grants simply has zero rows in <c>user_permissions</c>.
/// Used exclusively by <see cref="PermissionProjection"/>.
/// </summary>
public sealed class PermissionEventApplier(
	IUserPermissionWriteRepository repository,
	RedisCache redisCache)
{
	public Task ApplyAsync(
		IIntegrationEvent @event,
		CancellationToken ct = default
	) => @event switch
	{
		UserPermissionCreatedEvent => Task.CompletedTask,
		PermissionGrantedEvent e => ApplyAsync(e: e, ct: ct),
		PermissionRevokedEvent e => ApplyAsync(e: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled integration event: {@event.GetType().Name}", eventType: @event.GetType())
	};

	private async Task ApplyAsync(
		PermissionGrantedEvent e,
		CancellationToken ct)
	{
		await repository.GrantAsync(new PermissionGranted(
			Id: e.EventId,
			UserId: e.UserId,
			GrantedBy: e.GrantedBy,
			Permission: e.Permission,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		), ct);

		await redisCache.DeleteBatchAsync(keys: [CachedUserPermissionReadRepository.KeyFor(userId: e.UserId)]);
	}

	private async Task ApplyAsync(
		PermissionRevokedEvent e,
		CancellationToken ct)
	{
		await repository.RevokeAsync(new PermissionRevoked(
			Id: e.EventId,
			UserId: e.UserId,
			RevokedBy: e.RevokedBy,
			Permission: e.Permission,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		), ct);

		await redisCache.DeleteBatchAsync(keys: [CachedUserPermissionReadRepository.KeyFor(userId: e.UserId)]);
	}
}
