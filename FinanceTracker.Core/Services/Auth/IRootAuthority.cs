namespace FinanceTracker.Core.Services.Auth;

/// <summary>
/// Identifies the omnipotent root user, who bypasses every permission check unconditionally
/// </summary>
public interface IRootAuthority
{
	Task<bool> IsRootAsync(
		Guid userId,
		CancellationToken ct = default
	);
}
