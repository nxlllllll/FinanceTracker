namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transaction.including_blocked")]
public sealed class IncludingException(string message) : DomainException(message: message);
