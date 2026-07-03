using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Converters.Json;

public sealed class NameJsonConverter : JsonConverter<Name>
{
	public override Name Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string raw = reader.GetString() ?? throw new JsonException(message: "Name value cannot be null.");
		return Name.Reconstitute(value: raw);
	}

	public override void Write(Utf8JsonWriter writer, Name value, JsonSerializerOptions options)
		=> writer.WriteStringValue(value: value.Value);
}
