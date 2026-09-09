namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;

/// <summary>Raised when a transfer is older than the cancellation window allows.</summary>
[ErrorCode(code: "transfer.cancellation_window_expired")]
public sealed class TransferCancellationWindowExpiredException(string message) : DomainException(message: message);
