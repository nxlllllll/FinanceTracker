namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidExchangeRateException(string message) : Exception(message: message);