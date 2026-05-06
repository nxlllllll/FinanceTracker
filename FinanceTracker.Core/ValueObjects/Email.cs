using System.Text.RegularExpressions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

public readonly record struct Email
{
	private static readonly Regex FormatRegex = new Regex(
		pattern: @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
		options: RegexOptions.Compiled | RegexOptions.IgnoreCase
	);

	public string Value { get; }

	public Email(string value)
		=> Value = value;

	public static Result<Email, DomainException> Create(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<Email, DomainException>.Failure(error: new EmailException(message: "The email cannot be empty.", email: value ?? String.Empty));

		string normalized = value.Trim().ToLowerInvariant();

		if (!FormatRegex.IsMatch(input: normalized))
			return Result<Email, DomainException>.Failure(error: new EmailException(message: "The email is invalid.", email: value));

		return Result<Email, DomainException>.Success(value: new Email(value: normalized));
	}

	public static implicit operator string(Email email)
		=> email.Value;
 
	public override string ToString()
		=> Value;
}