using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.ValueObjects;

[JsonConverter(converterType: typeof(NameJsonConverter))]
public readonly record struct Name
{
	public string Value { get; }

	private Name(string value)
		=> Value = value;

	public static Result<Name, DomainException> Create(string value)
	{
		if (String.IsNullOrWhiteSpace(value: value))
			return Result<Name, DomainException>.Failure(error: new NameException(message: "The name cannot be empty."));

		return Result<Name, DomainException>.Success(value: new Name(value: value.Trim()));
	}

	public static Name Reconstitute(string value)
		=> new Name(value: value);

	public static implicit operator string(Name name)
		=> name.Value;

	public override string ToString()
		=> Value;
}