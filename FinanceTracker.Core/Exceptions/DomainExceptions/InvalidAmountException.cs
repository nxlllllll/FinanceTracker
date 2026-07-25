namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "validation.invalid_amount")]
public sealed class InvalidAmountException(string message) : DomainException(message: message);
