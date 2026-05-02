namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidExchangeRateException(string message) : DomainException(message: message);