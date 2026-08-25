namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;

/// <summary>
/// Raised when a transaction that has already been cancelled is asked to change again — including a
/// second cancellation.
/// </summary>
[ErrorCode(code: "transaction.cancelled")]
public sealed class CancelledOperationException(string message) : DomainException(message: message);
