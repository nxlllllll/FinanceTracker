namespace FinanceTracker.Core.Exceptions;

public abstract class DomainException(string message) : Exception(message: message);