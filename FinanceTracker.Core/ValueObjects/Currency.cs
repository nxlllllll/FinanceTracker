using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Immutable value object representing an ISO 4217 currency code (e.g. <c>USD</c>, <c>RUB</c>).
/// Normalises the input to uppercase. Use <see cref="Create"/> for user-supplied values
/// and <see cref="Reconstitute"/> when loading from storage.
/// </summary>
[JsonConverter(converterType: typeof(CurrencyJsonConverter))]
public readonly partial record struct Currency
{
	[GeneratedRegex(pattern: "^[A-Z]{3}$", options: RegexOptions.Compiled)]
	private static partial Regex CurrencyRegex();
	
	/// <summary>The normalised 3-letter currency code.</summary>
	public string Value { get; }

	private Currency(string value)
		=> Value = value;

	/// <summary>
	/// Creates a <see cref="Currency"/> from a string. Fails if the value is empty
	/// or does not match the 3-letter ISO 4217 format.
	/// </summary>
	public static Result<Currency, DomainException> Create(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<Currency, DomainException>.Failure(error: new CurrencyException(message: "The currency code cannot be empty."));

		string normalized = value.Trim().ToUpperInvariant();

		if (!CurrencyRegex().IsMatch(input: normalized))
			return Result<Currency, DomainException>.Failure(error: new CurrencyException(message: "The currency code is invalid. Expected 3 uppercase letters (e.g. 'USD')."));

		return Result<Currency, DomainException>.Success(value: new Currency(value: normalized));
	}

	/// <summary>Bypasses validation. Use only when loading from a trusted storage source.</summary>
	public static Currency Reconstitute(string value)
		=> new Currency(value: value);

	/// <summary>Implicit conversion to <see cref="string"/> for convenience in comparisons and logging.</summary>
	public static implicit operator string(Currency code)
		=> code.Value;

	/// <inheritdoc/>
	/// <returns>Returns a string representation of the money, for example, <c>100 RUB</c></returns>
	public override string ToString()
		=> Value;
}