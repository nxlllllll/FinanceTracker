using System.Text.RegularExpressions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Email
{
	private static readonly Regex FormatRegex = new Regex(
		pattern: @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
		options: RegexOptions.Compiled | RegexOptions.IgnoreCase
	);
 
	public string Value { get; }
 
	public Email(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			throw new EmailException(message: "The email cannot be empty.", email: value ?? String.Empty);
 
		string normalized = value.Trim().ToLowerInvariant();
 
		if (!FormatRegex.IsMatch(input: normalized))
			throw new EmailException(message: "The email is invalid.", email: value);
 
		Value = normalized;
	}
 
	public static implicit operator string(Email email)
		=> email.Value;
 
	public static implicit operator Email(string value)
		=> new Email(value);
 
	public override string ToString()
		=> Value;
}