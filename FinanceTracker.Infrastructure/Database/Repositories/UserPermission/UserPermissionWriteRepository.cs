using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserPermission;

public sealed class UserPermissionWriteRepository(FinanceTrackerContext context) : IUserPermissionWriteRepository
{
	public Task GrantAsync(
		PermissionGranted @event,
		CancellationToken ct = default
	) => context.InsertUserPermissionAsync(
		userId: @event.UserId,
		permission: @event.Permission,
		grantedAt: @event.OccurredAt,
		ct: ct
	);

	public Task RevokeAsync(
		Guid userId,
		string permission,
		CancellationToken ct = default
	) => context.UserPermissions.Where(predicate: e => e.UserId == userId && e.Permission == permission).ExecuteDeleteAsync(cancellationToken: ct);
}
