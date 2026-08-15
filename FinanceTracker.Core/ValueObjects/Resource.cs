namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// A permission-checkable resource. Adding a new value here is a code change on its own —
/// see <see cref="ValueObjects.Permission.Catalog"/> for which actions each resource actually supports.
/// </summary>
public enum Resource
{
	Account,
	Balance,
	Transaction,
	Transfer,
	Budget,
	Category,
	RecurringTransaction,
	Permission
}
