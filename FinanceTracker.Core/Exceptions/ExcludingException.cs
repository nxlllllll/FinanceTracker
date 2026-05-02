namespace FinanceTracker.Core.Exceptions;

public sealed class ExcludingException(string message) : DomainException(message: message);