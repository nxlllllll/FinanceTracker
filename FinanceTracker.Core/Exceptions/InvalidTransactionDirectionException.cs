namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidTransactionDirectionException(string message) : Exception(message: message);