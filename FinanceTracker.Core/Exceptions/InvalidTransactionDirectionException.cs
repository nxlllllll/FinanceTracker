namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidTransactionDirectionException(string message) : DomainException(message: message);