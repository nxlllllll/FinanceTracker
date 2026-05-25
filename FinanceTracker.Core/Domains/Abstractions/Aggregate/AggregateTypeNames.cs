namespace FinanceTracker.Core.Domains.Abstractions.Aggregate;

public static class AggregateTypeNames
{
	public const string Account = nameof(Account);
	public const string Transaction = nameof(Transaction);
	public const string RecurringTransaction = nameof(RecurringTransaction);
	public const string Transfer = nameof(Transfer);
	public const string Budget = nameof(Budget);
	public const string Category = nameof(Category);
	public const string User = nameof(User);
}
