namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;

[ErrorCode(code: "transaction.invalid_direction")]
public sealed class InvalidTransactionDirectionException(string message) : DomainException(message: message);
