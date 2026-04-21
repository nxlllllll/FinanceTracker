namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidInitialBalanceException(string message) : Exception(message: message);