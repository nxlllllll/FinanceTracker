using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

/// <summary>
/// Thrown when an exchange rate for a currency pair is completely missing — neither an exact
/// rate for the requested date nor any historical rate exists
/// </summary>
public sealed class CurrencyRateMissingException(
	string message,
	Currency fromCurrency,
	Currency toCurrency
) : ConfigurationException(message: message)
{
	public Currency FromCurrency { get; init; } = fromCurrency;
	public Currency ToCurrency { get; init; } = toCurrency;
}
