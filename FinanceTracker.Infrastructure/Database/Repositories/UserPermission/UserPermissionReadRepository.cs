using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserPermission;

public sealed class UserPermissionReadRepository(FinanceTrackerContext context) : IUserPermissionReadRepository
{
	public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
	{
		IQueryable<string> directGrants = context.UserPermissions.AsNoTracking()
			.Where(predicate: e => e.UserId == userId && e.IsActive)
			.Select(selector: e => e.Permission);

		IQueryable<string> roleGrants = context.UserRoles.AsNoTracking().Where(predicate: ur => ur.UserId == userId && ur.IsActive).Join(
			inner: context.RolePermissions,
			outerKeySelector: ur => ur.RoleId,
			innerKeySelector: rp => rp.RoleId,
			resultSelector: (ur, rp) => rp.Permission
		);

		return await directGrants.Union(source2: roleGrants).ToHashSetAsync(cancellationToken: ct);
	}
}
