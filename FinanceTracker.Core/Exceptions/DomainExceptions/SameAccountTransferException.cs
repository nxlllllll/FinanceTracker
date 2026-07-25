namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transfer.same_account")]
public sealed class SameAccountTransferException(string message) : DomainException(message: message);
