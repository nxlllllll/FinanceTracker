namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ExcludedOperationException(string message) : DomainException(message: message);