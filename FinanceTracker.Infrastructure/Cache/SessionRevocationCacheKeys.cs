namespace FinanceTracker.Infrastructure.Cache;

public static class SessionRevocationCacheKeys
{
	public static string RevokedSessionKey(Guid sessionId)
		=> $"revoked-session:{sessionId}";
}
