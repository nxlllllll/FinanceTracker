using FinanceTracker.Core.Repositories.UserPermission;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.UserPermission;

public sealed class UserPermissionReadRepository(FinanceTrackerContext context) : IUserPermissionReadRepository
{
	public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
	{
		return await context.UserPermissions.AsNoTracking()
			.Where(predicate: e => e.UserId == userId)
			.Select(selector: e => e.Permission)
			.ToHashSetAsync(cancellationToken: ct);
	}
}
