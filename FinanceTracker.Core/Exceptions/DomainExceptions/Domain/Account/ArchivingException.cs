namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;

[ErrorCode(code: "account.archiving_blocked")]
public sealed class ArchivingException(string message) : DomainException(message: message);
