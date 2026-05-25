namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidExchangeRateException(string message) : DomainException(message: message);
