namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Currency;

/// <summary>No currency is registered under the given code.</summary>
[ErrorCode(code: "currency.not_found")]
public sealed class CurrencyNotFoundException(string message, string code) : DomainException(message: message)
{
	public string Code { get; init; } = code;
}
