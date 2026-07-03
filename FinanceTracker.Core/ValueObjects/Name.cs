using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Immutable value object representing a non-empty display name with a maximum length.
/// Trims surrounding whitespace on creation. Use <see cref="Create"/> for user-supplied values
/// and <see cref="Reconstitute"/> when loading from storage.
/// </summary>
[JsonConverter(converterType: typeof(NameJsonConverter))]
public readonly record struct Name
{
	private const int MaxLength = 100;

	/// <summary>The trimmed name value.</summary>
	public string Value { get; }

	private Name(string value)
		=> Value = value;

	/// <summary>
	/// Creates a <see cref="Name"/> from a string. Fails if the value is empty
	/// or exceeds <c>100</c> characters after trimming.
	/// </summary>
	public static Result<Name, DomainException> Create(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<Name, DomainException>.Failure(error: new NameException(message: "The name cannot be empty."));

		string trimmed = value.Trim();

		if (trimmed.Length > MaxLength)
			return Result<Name, DomainException>.Failure(error: new NameException(message: $"The name cannot exceed {MaxLength} characters."));

		return Result<Name, DomainException>.Success(value: new Name(value: trimmed));
	}

	/// <summary>Bypasses validation. Use only when loading from a trusted storage source.</summary>
	public static Name Reconstitute(string value)
		=> new Name(value: value);

	/// <summary>Implicit conversion to <see cref="string"/> for convenience in comparisons and logging.</summary>
	public static implicit operator string(Name name)
		=> name.Value;

	/// <inheritdoc/>
	/// <returns>Returns a string representation of the name</returns>
	public override string ToString()
		=> Value;
}
