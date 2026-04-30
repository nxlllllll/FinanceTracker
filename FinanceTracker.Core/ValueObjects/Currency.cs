using System.Text.Json.Serialization;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Currency
{
	public string Value { get; }
 
	[JsonConstructor]
	public Currency(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			throw new CurrencyException(message: "The currency code cannot be empty.");
 
		Value = value.ToUpperInvariant();
	}
 
	public static implicit operator string(Currency code)
		=> code.Value;
	
	public static implicit operator Currency(string value)
		=> new Currency(value: value);
 
	public override string ToString() 
		=> Value;
}
