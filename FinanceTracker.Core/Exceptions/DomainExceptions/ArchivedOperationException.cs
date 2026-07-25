namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "account.archived")]
public sealed class ArchivedOperationException(string message) : DomainException(message: message);
