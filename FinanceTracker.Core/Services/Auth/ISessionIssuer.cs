namespace FinanceTracker.Core.Services.Auth;

public interface ISessionIssuer
{
	Task<SessionToken> IssueAsync(
		Domains.User.User user,
		CancellationToken ct = default
	);
}
