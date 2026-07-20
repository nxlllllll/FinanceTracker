namespace FinanceTracker.Core.Repositories.Role;

/// <summary>
/// Flat read model for the hot path of role checks (root bypass, future role-gated features)
/// </summary>
public interface IUserRoleReadRepository
{
	Task<bool> HasSystemRoleAsync(
		Guid userId,
		string systemKey,
		CancellationToken ct = default
	);
}
