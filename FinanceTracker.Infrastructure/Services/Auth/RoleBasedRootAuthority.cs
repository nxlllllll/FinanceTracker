using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Services.Auth;

public sealed class RoleBasedRootAuthority(
	IUserRoleReadRepository userRoleReadRepository
) : IRootAuthority
{
	private static readonly string RootSystemKey = nameof(SystemRole.Root).ToLowerInvariant();

	public async Task<bool> IsRootAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		if (userId == Guid.Empty)
			return false;

		return await userRoleReadRepository.HasSystemRoleAsync(userId: userId, systemKey: RootSystemKey, ct: ct);
	}
}
