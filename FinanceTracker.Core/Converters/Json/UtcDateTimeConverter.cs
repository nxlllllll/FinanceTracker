using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinanceTracker.Core.Converters.Json;

public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		=> DateTime.SpecifyKind(value: reader.GetDateTime(), kind: DateTimeKind.Utc);

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
		=> writer.WriteStringValue(value: value);
}