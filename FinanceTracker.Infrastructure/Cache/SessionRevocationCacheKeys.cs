namespace FinanceTracker.Infrastructure.Cache;

internal static class SessionRevocationCacheKeys
{
	public static string RevokedSessionKey(Guid sessionId)
		=> $"revoked-session:{sessionId}";
}
