namespace FinanceTracker.Core.Exceptions;

public sealed class NegativeInitialBalanceException(string message) : Exception(message: message);