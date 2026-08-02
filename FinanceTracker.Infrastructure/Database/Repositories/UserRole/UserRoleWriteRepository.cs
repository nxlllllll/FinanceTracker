using FinanceTracker.Core.Domains.UserRole.Events;
using FinanceTracker.Core.Repositories.UserRole;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;

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
}
