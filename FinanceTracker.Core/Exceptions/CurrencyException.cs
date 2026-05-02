namespace FinanceTracker.Core.Exceptions;

public sealed class CurrencyException(string message) : DomainException(message: message);