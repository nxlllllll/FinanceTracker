using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Exceptions.TransientExceptions;

/// <summary>
/// Thrown when an exchange rate for a currency pair is completely missing — neither an exact
/// rate for the requested date nor any historical one exists.
/// </summary>
[ErrorCode(code: "currency.rate_unavailable")]
public sealed class CurrencyRateMissingException(
	string message,
	Currency fromCurrency,
	Currency toCurrency
) : TransientException(message: message, retryAfterSeconds: DefaultRetryAfterSeconds)
{
	private const int DefaultRetryAfterSeconds = 60;
	public Currency FromCurrency { get; init; } = fromCurrency;
	public Currency ToCurrency { get; init; } = toCurrency;
}
