namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class PasswordException(string message) : DomainException(message: message);
