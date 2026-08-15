using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.UserRole;
using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Infrastructure.Cache;

namespace FinanceTracker.Worker.UserRoleProjection.Projection;

public sealed class UserRoleEventApplier(
	IUserRoleWriteRepository repository,
	RedisCache redisCache,
	IUnitOfWork unitOfWork)
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
		await repository.AssignAsync(@event: new RoleAssigned(
			Id: e.EventId,
			UserId: e.UserId,
			RoleId: e.RoleId,
			AssignedBy: e.AssignedBy,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		), ct: ct);

		ScheduleCacheInvalidation(userId: e.UserId);
	}

	private async Task ApplyAsync(
		RoleRemovedEvent e,
		CancellationToken ct)
	{
		await repository.RemoveAsync(@event: new RoleRemoved(
			Id: e.EventId,
			UserId: e.UserId,
			RoleId: e.RoleId,
			RemovedBy: e.RemovedBy,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		), ct: ct);

		ScheduleCacheInvalidation(userId: e.UserId);
	}

	private void ScheduleCacheInvalidation(
		Guid userId
	) => unitOfWork.OnCommitted(callback: () => redisCache.DeleteBatchAsync(
		keys: PermissionCacheKeys.AllForUser(userId: userId)
	));
}
