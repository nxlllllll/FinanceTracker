using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Role;

public sealed class UserRoleReadRepository(
	FinanceTrackerContext context
) : IUserRoleReadRepository
{
	public async Task<bool> HasSystemRoleAsync(Guid userId, string systemKey, CancellationToken ct = default)
	{
		return await context.UserRoles.Join(
			inner: context.Roles,
			outerKeySelector: ur => ur.RoleId,
			innerKeySelector: r => r.Id,
			resultSelector: (ur, r) => new { ur.UserId, r.SystemKey }
		).AnyAsync(predicate: x => x.UserId == userId && x.SystemKey == systemKey, cancellationToken: ct);
	}
}
