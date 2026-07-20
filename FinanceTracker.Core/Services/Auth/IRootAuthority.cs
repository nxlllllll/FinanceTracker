namespace FinanceTracker.Core.Services.Auth;

/// <summary>
/// Identifies the omnipotent root user, who bypasses every permission check unconditionally
/// </summary>
public interface IRootAuthority
{
	bool IsRoot(Guid userId);
}
