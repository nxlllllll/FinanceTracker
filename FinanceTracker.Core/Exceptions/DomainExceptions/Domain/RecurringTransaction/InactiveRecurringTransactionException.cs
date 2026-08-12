namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.RecurringTransaction;

/// <summary>
/// Raised when an operation is attempted on an inactive recurring transaction.
/// Distinct from <see cref="DeactivatingException"/>, which means "already inactive
/// and you tried to deactivate again".
/// </summary>
[ErrorCode(code: "recurring_transaction.inactive")]
public sealed class InactiveRecurringTransactionException(string message) : DomainException(message: message);
