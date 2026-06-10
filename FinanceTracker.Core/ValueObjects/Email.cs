using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Immutable value object representing a validated email address.
/// Normalises the input to lowercase. Use <see cref="Create"/> for user-supplied values
/// and <see cref="Reconstitute"/> when loading from storage.
/// </summary>
[JsonConverter(converterType: typeof(EmailJsonConverter))]
public readonly partial record struct Email
{
	[GeneratedRegex(pattern: @"^[^@\s]+@[^@\s]+\.[^@\s]+$", options: RegexOptions.IgnoreCase | RegexOptions.Compiled, cultureName: "ru-RU")]
	private static partial Regex EmailRegex();
	private static readonly Regex FormatRegex = EmailRegex();

	/// <summary>The normalised, lowercase email address.</summary>
	public string Value { get; }

	private Email(string value)
		=> Value = value;

	/// <summary>
	/// Creates an <see cref="Email"/> from a string. Fails if the value is empty
	/// or does not match a basic email format.
	/// </summary>
	public static Result<Email, DomainException> Create(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<Email, DomainException>.Failure(error: new EmailException(message: "The email cannot be empty.", email: value ?? String.Empty));

		string normalized = value.Trim().ToLowerInvariant();

		if (!FormatRegex.IsMatch(input: normalized))
			return Result<Email, DomainException>.Failure(error: new EmailException(message: "The email is invalid.", email: value));

		return Result<Email, DomainException>.Success(value: new Email(value: normalized));
	}

	/// <summary>Bypasses validation. Use only when loading from a trusted storage source.</summary>
	public static Email Reconstitute(string value)
		=> new Email(value: value);

	/// <summary>Implicit conversion to <see cref="string"/> for convenience in comparisons and logging.</summary>
	public static implicit operator string(Email email)
		=> email.Value;

	/// <inheritdoc/>
	/// <returns>Returns a string representation of the email, for example, <c>test@gmail.com</c></returns>
	public override string ToString()
		=> Value;
}