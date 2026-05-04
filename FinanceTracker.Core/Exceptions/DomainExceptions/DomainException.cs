namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public abstract class DomainException(string message) : Exception(message: message);