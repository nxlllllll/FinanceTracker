namespace FinanceTracker.Core.Exceptions.DomainExceptions;

/// <summary>
/// Raised when an operation is attempted on an inactive recurring transaction.
/// Distinct from <see cref="DeactivatingException"/>, which means "already inactive
/// and you tried to deactivate again".
/// </summary>
public sealed class InactiveRecurringTransactionException(string message) : DomainException(message: message);