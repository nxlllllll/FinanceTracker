namespace FinanceTracker.Core.Exceptions.DomainExceptions.Validation;

[ErrorCode(code: "validation.invalid_time_zone")]
public sealed class TimeZoneException(string message) : DomainException(message: message);
