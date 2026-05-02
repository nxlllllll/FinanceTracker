namespace FinanceTracker.Core.Exceptions;

public sealed class PasswordException(string message) : DomainException(message: message);