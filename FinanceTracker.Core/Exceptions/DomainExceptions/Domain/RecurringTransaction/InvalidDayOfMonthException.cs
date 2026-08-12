namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.RecurringTransaction;

[ErrorCode(code: "recurring_transaction.invalid_day_of_month")]
public sealed class InvalidDayOfMonthException(string message) : DomainException(message: message);
