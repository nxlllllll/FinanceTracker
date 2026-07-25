namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transaction.excluded")]
public sealed class ExcludedOperationException(string message) : DomainException(message: message);
