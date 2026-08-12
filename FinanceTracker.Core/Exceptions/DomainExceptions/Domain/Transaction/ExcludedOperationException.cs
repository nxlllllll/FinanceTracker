namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;

[ErrorCode(code: "transaction.excluded")]
public sealed class ExcludedOperationException(string message) : DomainException(message: message);
