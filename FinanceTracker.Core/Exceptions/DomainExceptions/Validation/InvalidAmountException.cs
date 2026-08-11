namespace FinanceTracker.Core.Exceptions.DomainExceptions.Validation;

[ErrorCode(code: "validation.invalid_amount")]
public sealed class InvalidAmountException(string message) : DomainException(message: message);
