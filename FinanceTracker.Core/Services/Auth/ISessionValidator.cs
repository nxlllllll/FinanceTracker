namespace FinanceTracker.Core.Services.Auth;

/// <summary>
/// Answers whether the session behind an access token is still usable.
/// </summary>
public interface ISessionValidator
{
	Task<bool> IsSessionActiveAsync(Guid sessionId, CancellationToken ct = default);
}
