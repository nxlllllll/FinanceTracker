namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class CurrencyException(string message) : DomainException(message: message);