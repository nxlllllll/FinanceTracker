using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Currency
{
	private static readonly Regex FormatRegex = new Regex(pattern: "^[A-Z]{3}$", options: RegexOptions.Compiled);
 
	public string Value { get; }
 
	[JsonConstructor]
	public Currency(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			throw new CurrencyException(message: "The currency code cannot be empty.");
 
		string normalized = value.Trim().ToUpperInvariant();
 
		if (!FormatRegex.IsMatch(input: normalized))
			throw new CurrencyException(message: "The currency code is invalid. Expected 3 uppercase letters (e.g. 'USD').");
 
		Value = normalized;
	}
 
	public static implicit operator string(Currency code)
		=> code.Value;
 
	public static implicit operator Currency(string value)
		=> new Currency(value: value);
 
	public override string ToString()
		=> Value;
}