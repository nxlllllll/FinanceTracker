namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidAmountException(string message) : Exception(message: message);