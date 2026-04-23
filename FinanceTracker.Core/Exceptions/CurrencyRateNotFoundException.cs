namespace FinanceTracker.Core.Exceptions;

public sealed class CurrencyRateNotFoundException(
	string message,
	string fromCurrency,
	string toCurrency
) : Exception(message: message)
{
	public string FromCurrency { get; init; } = fromCurrency;
	public string ToCurrency { get; init; } = toCurrency;
}