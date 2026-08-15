namespace FinanceTracker.Api.Http;

public static class QueryTime
{
	public static DateTimeOffset? ToUtc(DateTimeOffset? instant) => instant?.ToUniversalTime();
}
