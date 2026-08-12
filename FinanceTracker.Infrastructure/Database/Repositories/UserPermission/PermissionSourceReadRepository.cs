using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserPermission;

public sealed class PermissionSourceReadRepository(FinanceTrackerContext context) : IPermissionSourceReadRepository
{
	public async Task<IReadOnlySet<string>> GetDirectGrantsAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.UserPermissions.AsNoTracking().Where(predicate: e => e.UserId == userId && e.IsActive)
			.Select(selector: e => e.Permission)
			.ToHashSetAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlySet<string>> GetRoleGrantsAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.UserRoles.AsNoTracking().Where(predicate: ur => ur.UserId == userId && ur.IsActive)
			.Join(
				inner: context.RolePermissions,
				outerKeySelector: ur => ur.RoleId,
				innerKeySelector: rp => rp.RoleId,
				resultSelector: (ur, rp) => rp.Permission
			).ToHashSetAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlySet<string>> GetPermissionsForRolesAsync(
		IReadOnlyCollection<Guid> roleIds,
		CancellationToken ct = default)
	{
		if (roleIds.Count == 0)
			return new HashSet<string>();

		return await context.RolePermissions.AsNoTracking()
			.Where(predicate: rp => roleIds.Contains(rp.RoleId))
			.Select(selector: rp => rp.Permission)
			.ToHashSetAsync(cancellationToken: ct);
	}
}
