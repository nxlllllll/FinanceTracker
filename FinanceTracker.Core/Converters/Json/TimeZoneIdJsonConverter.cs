using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Converters.Json;

public sealed class TimeZoneIdJsonConverter : JsonConverter<TimeZoneId>
{
	public override TimeZoneId Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		string raw = reader.GetString() ?? throw new JsonException(message: "Time zone id cannot be null.");
		return TimeZoneId.Reconstitute(value: raw);
	}

	public override void Write(
		Utf8JsonWriter writer,
		TimeZoneId value,
		JsonSerializerOptions options
	) => writer.WriteStringValue(value: value.Value);
}
