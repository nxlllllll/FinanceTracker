namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidTokenException(string message = "Token is invalid, expired or revoked.") : DomainException(message: message);
