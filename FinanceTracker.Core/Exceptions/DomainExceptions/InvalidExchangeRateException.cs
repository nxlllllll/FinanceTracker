namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "validation.invalid_exchange_rate")]
public sealed class InvalidExchangeRateException(string message) : DomainException(message: message);
