namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidTransactionDirectionException(string message) : DomainException(message: message);
