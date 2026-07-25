namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transfer.invalid_status")]
public sealed class InvalidTransferStatusException(string message) : DomainException(message: message);
