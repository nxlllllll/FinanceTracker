using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.UserRole;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Infrastructure.Cache;

namespace FinanceTracker.Worker.UserRoleProjection.Projection;

public sealed class UserRoleEventApplier(
	IUserRoleWriteRepository repository,
	RedisCache redisCache)
{
	public Task ApplyAsync(
		IIntegrationEvent @event,
		CancellationToken ct = default
	) => @event switch
	{
		UserRoleCreatedEvent => Task.CompletedTask,
		RoleAssignedEvent e => ApplyAsync(e: e, ct: ct),
		RoleRemovedEvent e => ApplyAsync(e: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled integration event: {@event.GetType().Name}", eventType: @event.GetType())
	};

	private async Task ApplyAsync(
		RoleAssignedEvent e,
		CancellationToken ct)
	{
		await repository.AssignAsync(new RoleAssigned(
			Id: e.EventId,
			UserId: e.UserId,
			RoleId: e.RoleId,
			AssignedBy: e.AssignedBy,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		), ct);

		await redisCache.DeleteBatchAsync(keys: [CachedUserPermissionReadRepository.KeyFor(userId: e.UserId)]);
	}

	private async Task ApplyAsync(
		RoleRemovedEvent e,
		CancellationToken ct)
	{
		await repository.RemoveAsync(new RoleRemoved(
			Id: e.EventId,
			UserId: e.UserId,
			RoleId: e.RoleId,
			RemovedBy: e.RemovedBy,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		), ct);

		await redisCache.DeleteBatchAsync(keys: [CachedUserPermissionReadRepository.KeyFor(userId: e.UserId)]);
	}
}
