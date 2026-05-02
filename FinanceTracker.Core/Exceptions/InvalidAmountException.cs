namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidAmountException(string message) : DomainException(message: message);