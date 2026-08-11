namespace FinanceTracker.Infrastructure.Cache;

public static class SessionCacheKeys
{
	/// <summary>
	/// Key holding the fact that a session was active as of the last database read.
	/// </summary>
	public static string ActiveSessionKey(Guid sessionId)
		=> $"active-session:{sessionId}";
}
