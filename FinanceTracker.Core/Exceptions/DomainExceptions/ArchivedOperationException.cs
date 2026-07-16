namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ArchivedOperationException(string message) : DomainException(message: message);
