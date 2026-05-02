namespace FinanceTracker.Core.Exceptions;

public sealed class IncludingException(string message) : DomainException(message: message);