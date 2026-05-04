namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class CurrencyRateNotFoundException(
	string message,
	string fromCurrency,
	string toCurrency
) : DomainException(message: message)
{
	public string FromCurrency { get; init; } = fromCurrency;
	public string ToCurrency { get; init; } = toCurrency;
}