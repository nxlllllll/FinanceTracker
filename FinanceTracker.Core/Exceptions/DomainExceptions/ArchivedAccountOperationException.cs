namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ArchivedAccountOperationException(string message) : DomainException(message: message);