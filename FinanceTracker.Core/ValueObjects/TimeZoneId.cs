using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Immutable value object representing time zone identifier, such as <c>Europe/Moscow</c>.
/// </summary>
[JsonConverter(converterType: typeof(TimeZoneIdJsonConverter))]
public readonly record struct TimeZoneId
{
	private const int MaxLength = 64;

	/// <summary>
	/// The identifier every user starts with, and the one a client that sends no time zone is given.
	/// </summary>
	public static TimeZoneId Utc => new TimeZoneId(value: "Etc/UTC");

	public string Value { get; }

	private TimeZoneId(string value)
		=> Value = value;

	public static Result<TimeZoneId, DomainException> Create(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<TimeZoneId, DomainException>.Failure(error: new TimeZoneException(message: "The time zone cannot be empty."));

		string trimmed = value.Trim();

		if (trimmed.Length > MaxLength)
			return Result<TimeZoneId, DomainException>.Failure(error: new TimeZoneException(message: $"The time zone cannot exceed {MaxLength} characters."));

		if (TimeZoneInfo.TryConvertWindowsIdToIanaId(windowsId: trimmed, ianaId: out string? _))
			return Result<TimeZoneId, DomainException>.Failure(error: new TimeZoneException(
				message: $"'{trimmed}' is a Windows time zone identifier. Use the IANA form, for example 'Europe/Moscow'."
			));

		if (!TimeZoneInfo.TryFindSystemTimeZoneById(id: trimmed, timeZoneInfo: out TimeZoneInfo? _))
			return Result<TimeZoneId, DomainException>.Failure(error: new TimeZoneException(
				message: $"'{trimmed}' is not a known time zone identifier."
			));

		return Result<TimeZoneId, DomainException>.Success(value: new TimeZoneId(value: trimmed));
	}

	public static TimeZoneId Reconstitute(string value)
		=> new TimeZoneId(value: value);

	/// <summary>
	/// Resolves the identifier against the host's time zone database.
	/// </summary>
	public TimeZoneInfo ToTimeZoneInfo()
		=> TimeZoneInfo.FindSystemTimeZoneById(id: Value);

	/// <summary>Implicit conversion to <see cref="string"/> for convenience in comparisons and logging.</summary>
	public static implicit operator string(TimeZoneId timeZoneId) => timeZoneId.Value ?? throw new InvalidOperationException(
		message: "Cannot convert a default(TimeZoneId) to a string — this time zone was never created through TimeZoneId.Create or TimeZoneId.Reconstitute."
	);

	/// <returns>Returns a string representation of the time zone identifier</returns>
	public override string ToString()
		=> Value;
}
