namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "auth.invalid_token")]
public sealed class InvalidTokenException(string message = "Token is invalid, expired or revoked.") : DomainException(message: message);
