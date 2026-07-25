namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transaction.excluding_blocked")]
public sealed class ExcludingException(string message) : DomainException(message: message);
