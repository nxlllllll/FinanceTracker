namespace FinanceTracker.Api.Routing;

/// <summary>Names given to endpoints that something else needs to link to.</summary>
public static class RouteNames
{
	public const string GetAccount = nameof(GetAccount);
	public const string GetBudget = nameof(GetBudget);
	public const string GetCategory = nameof(GetCategory);
	public const string GetRole = nameof(GetRole);
	public const string GetRecurringTransaction = nameof(GetRecurringTransaction);
	public const string GetTransaction = nameof(GetTransaction);
	public const string GetTransfer = nameof(GetTransfer);
}
