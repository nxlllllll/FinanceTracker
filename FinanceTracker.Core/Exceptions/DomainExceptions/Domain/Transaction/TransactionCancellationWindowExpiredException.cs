namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;

/// <summary>
/// Raised when a transaction is older than the cancellation window allows.
/// </summary>
[ErrorCode(code: "transaction.cancellation_window_expired")]
public sealed class TransactionCancellationWindowExpiredException(string message) : DomainException(message: message);
