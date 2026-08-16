using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Converters.Json;

public sealed class MoneyJsonConverter : JsonConverter<Money>
{
	public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
			throw new JsonException(message: "Expected start of object for Money.");

		decimal amount = 0;
		Currency currency = default;

		while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
		{
			if (reader.TokenType != JsonTokenType.PropertyName)
				throw new JsonException(message: "Expected property name.");

			string? propertyName = reader.GetString();
			reader.Read();

			switch (propertyName)
			{
				case nameof(Money.Amount):
				case "amount":
					amount = reader.GetDecimal();
					break;
				case nameof(Money.Currency):
				case "currency":
					currency = JsonSerializer.Deserialize<Currency>(ref reader, options: options);
					break;
			}
		}

		if (currency == default)
		{
			throw new JsonException(message:
				$"'{nameof(Money.Currency)}' is missing. A stored amount without its currency is not a value this " +
				$"type can represent, and defaulting it would put a zero-currency Money into an aggregate."
			);
		}

		return Money.Reconstitute(amount: amount, currency: currency);
	}

	public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		writer.WriteNumber(propertyName: PropertyName(name: nameof(Money.Amount), options: options), value: value.Amount);
		writer.WriteString(propertyName: PropertyName(name: nameof(Money.Currency), options: options), value: value.Currency.Value);
		writer.WriteEndObject();
	}

	private static string PropertyName(string name, JsonSerializerOptions options)
		=> options.PropertyNamingPolicy?.ConvertName(name: name) ?? name;
}
