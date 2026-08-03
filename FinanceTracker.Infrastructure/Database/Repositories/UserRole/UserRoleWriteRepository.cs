using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserRole;

public sealed class UserRoleWriteRepository(FinanceTrackerContext context) : IUserRoleWriteRepository
{
	public Task AssignAsync(
		RoleAssigned @event,
		CancellationToken ct = default
	) => context.AssignUserRoleAsync(
		userId: @event.UserId,
		roleId: @event.RoleId,
		assignedBy: @event.AssignedBy,
		assignedAt: @event.OccurredAt,
		version: @event.Version,
		ct: ct
	);

	public Task RemoveAsync(
		RoleRemoved @event,
		CancellationToken ct = default
	) => context.RemoveUserRoleAsync(
		userId: @event.UserId,
		roleId: @event.RoleId,
		removedBy: @event.RemovedBy,
		removedAt: @event.OccurredAt,
		version: @event.Version,
		ct: ct
	);

	public async Task<int> DeleteOldTombstonesAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.UserRoles.Where(predicate: e => !e.IsActive && e.RemovedAt < before)
			.OrderBy(keySelector: e => e.RemovedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}
