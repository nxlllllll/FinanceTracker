namespace FinanceTracker.Core.Exceptions.DomainExceptions.Validation;

[ErrorCode(code: "validation.invalid_currency")]
public sealed class CurrencyException(string message) : DomainException(message: message);
