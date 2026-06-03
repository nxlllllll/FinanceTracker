using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class CurrencyRateNotFoundException(
	string message,
	Currency fromCurrency,
	Currency toCurrency
) : DomainException(message: message)
{
	public Currency FromCurrency { get; init; } = fromCurrency;
	public Currency ToCurrency { get; init; } = toCurrency;
}
