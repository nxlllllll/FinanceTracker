namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "validation.invalid_name")]
public sealed class NameException(string message) : DomainException(message: message);
