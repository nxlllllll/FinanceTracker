namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "validation.invalid_password")]
public sealed class PasswordException(string message) : DomainException(message: message);
