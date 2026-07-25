namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "auth.invalid_credentials")]
public sealed class InvalidCredentialsException(string message = "Invalid email or password.") : DomainException(message: message);
