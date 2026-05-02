namespace FinanceTracker.Core.Exceptions;

public sealed class DeactivatingException(string message) : DomainException(message: message);