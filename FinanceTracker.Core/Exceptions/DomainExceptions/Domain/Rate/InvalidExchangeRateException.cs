namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Rate;

[ErrorCode(code: "rate.invalid_exchange_rate")]
public sealed class InvalidExchangeRateException(string message) : DomainException(message: message);
