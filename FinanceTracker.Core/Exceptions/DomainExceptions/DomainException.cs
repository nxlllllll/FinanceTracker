namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public abstract class DomainException(string message) : AppException(message: message);