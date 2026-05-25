using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Converters.Json;

public sealed class ResultJsonConverter<TValue, TError> : JsonConverter<Result<TValue, TError>>
	where TError : AppException
{
	public override Result<TValue, TError> Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options)
	{
		using JsonDocument doc = JsonDocument.ParseValue(ref reader);
 
		bool isSuccess = doc.RootElement.GetProperty(propertyName: "IsSuccess").GetBoolean();
 
		if (!isSuccess)
			throw new JsonException(message: "Cannot reconstruct a failed Result<> from idempotency cache. Only successful results should be cached.");
 
		TValue? value = doc.RootElement.GetProperty(propertyName: "Value").Deserialize<TValue>(options: options);
 
		return Result<TValue, TError>.Success(value: value!);
	}
 
	public override void Write(
		Utf8JsonWriter writer,
		Result<TValue, TError> value,
		JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteBoolean(propertyName: "IsSuccess", value: value.IsSuccess);
		writer.WritePropertyName(propertyName: "Value");
		JsonSerializer.Serialize(writer: writer, value: value.Value, options: options);
		writer.WriteEndObject();
	}
}
