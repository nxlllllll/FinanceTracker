namespace FinanceTracker.Core.Exceptions;

public sealed class ArchivedAccountOperationException(string message) : DomainException(message: message);