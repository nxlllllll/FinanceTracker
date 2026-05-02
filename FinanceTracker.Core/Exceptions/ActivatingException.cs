namespace FinanceTracker.Core.Exceptions;

public sealed class ActivatingException(string message) : DomainException(message: message);