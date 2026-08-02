using FinanceTracker.Core.Domains.UserPermission.Events;
using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserPermission;

public sealed class UserPermissionWriteRepository(FinanceTrackerContext context) : IUserPermissionWriteRepository
{
	public Task GrantAsync(
		PermissionGranted @event,
		CancellationToken ct = default
	) => context.GrantUserPermissionAsync(
		userId: @event.UserId,
		permission: @event.Permission,
		grantedAt: @event.OccurredAt,
		version: @event.Version,
		ct: ct
	);

	public Task RevokeAsync(
		PermissionRevoked @event,
		CancellationToken ct = default
	) => context.RevokeUserPermissionAsync(
		userId: @event.UserId,
		permission: @event.Permission,
		revokedAt: @event.OccurredAt,
		version: @event.Version,
		ct: ct
	);
}
