using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Converters.Json;

public sealed class CurrencyJsonConverter : JsonConverter<Currency>
{
	public override Currency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string raw = reader.GetString() ?? throw new JsonException(message: "Currency value cannot be null.");
		return Currency.Reconstitute(value: raw);
	}

	public override void Write(Utf8JsonWriter writer, Currency value, JsonSerializerOptions options)
		=> writer.WriteStringValue(value: value.Value);
}
