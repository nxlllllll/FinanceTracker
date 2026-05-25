using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Converters.Json;

public sealed class EmailJsonConverter : JsonConverter<Email>
{
	public override Email Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string raw = reader.GetString() ?? throw new JsonException(message: "Email value cannot be null.");

		Result<Email, DomainException> result = Email.Create(value: raw);
		if (result.IsFailure)
			throw new JsonException(message: result.Error!.Message);

		return result.Value;
	}

	public override void Write(Utf8JsonWriter writer, Email value, JsonSerializerOptions options)
		=> writer.WriteStringValue(value: value.Value);
}
