namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "transaction.invalid_direction")]
public sealed class InvalidTransactionDirectionException(string message) : DomainException(message: message);
