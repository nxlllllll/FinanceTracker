namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.RecurringTransaction;

/// <summary>
/// Raised when a change is attempted on a recurring transaction that is no longer active
/// </summary>
[ErrorCode(code: "recurring_transaction.inactive")]
public sealed class InactiveRecurringTransactionException(string message) : DomainException(message: message);
