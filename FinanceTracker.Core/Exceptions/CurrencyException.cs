namespace FinanceTracker.Core.Exceptions;

public sealed class CurrencyException(string message) : Exception(message: message);