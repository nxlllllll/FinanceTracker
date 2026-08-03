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

	public async Task<int> DeleteOldTombstonesAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.UserPermissions.Where(predicate: e => !e.IsActive && e.RevokedAt < before)
			.OrderBy(keySelector: e => e.RevokedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}
