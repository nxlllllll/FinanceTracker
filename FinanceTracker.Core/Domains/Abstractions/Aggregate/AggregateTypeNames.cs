namespace FinanceTracker.Core.Domains.Abstractions.Aggregate;

/// <summary>
/// Stable string constants used as aggregate type discriminators in the event store.
/// These values are persisted in the database and must never be renamed.
/// </summary>
public static class AggregateTypeNames
{
	public const string Account = nameof(Account);
	public const string Transaction = nameof(Transaction);
	public const string RecurringTransaction = nameof(RecurringTransaction);
	public const string Transfer = nameof(Transfer);
	public const string Budget = nameof(Budget);
	public const string Category = nameof(Category);
	public const string User = nameof(User);
	public const string UserPermission = nameof(UserPermission);
	public const string UserRole = nameof(UserRole);
}
