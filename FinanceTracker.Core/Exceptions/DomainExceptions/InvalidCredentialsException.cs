namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidCredentialsException(string message = "Invalid email or password.") : DomainException(message: message);