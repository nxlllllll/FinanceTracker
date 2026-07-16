namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidAmountException(string message) : DomainException(message: message);
