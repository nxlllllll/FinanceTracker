namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;

[ErrorCode(code: "account.archived")]
public sealed class ArchivedOperationException(string message) : DomainException(message: message);
