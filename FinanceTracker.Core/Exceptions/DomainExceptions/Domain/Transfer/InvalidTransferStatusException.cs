namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;

[ErrorCode(code: "transfer.invalid_status")]
public sealed class InvalidTransferStatusException(string message) : DomainException(message: message);
