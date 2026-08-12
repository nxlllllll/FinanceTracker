namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;

[ErrorCode(code: "transfer.same_account")]
public sealed class SameAccountTransferException(string message) : DomainException(message: message);
