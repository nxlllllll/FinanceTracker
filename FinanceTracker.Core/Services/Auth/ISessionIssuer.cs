using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Services.Auth;

public interface ISessionIssuer
{
	Task<TokenResponse> IssueAsync(
		Domains.User.User user,
		CancellationToken ct = default
	);
}