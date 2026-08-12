namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;

[ErrorCode(code: "auth.invalid_credentials")]
public sealed class InvalidCredentialsException(string message = "Invalid email or password.") : DomainException(message: message);
