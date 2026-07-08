using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.Converters.Json;

public sealed class MoneyJsonConverterTests
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		Converters = { new MoneyJsonConverter(), new CurrencyJsonConverter() }
	};

	[Test]
	public async Task Write_ShouldWriteAmountAndCurrencyAsObject()
	{
		Money money = Money.Reconstitute(amount: 42.5m, currency: Currency.Reconstitute(value: "USD"));

		string json = JsonSerializer.Serialize(value: money, options: Options);
		using JsonDocument doc = JsonDocument.Parse(json: json);

		await Assert.That(value: doc.RootElement.GetProperty(propertyName: "Amount").GetDecimal()).IsEqualTo(expected: 42.5m);
		await Assert.That(value: doc.RootElement.GetProperty(propertyName: "Currency").GetString()).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task Read_ShouldReconstituteMoneyFromObject()
	{
		Money money = JsonSerializer.Deserialize<Money>(json: """{"Amount":10.25,"Currency":"EUR"}""", options: Options);

		await Assert.That(value: money.Amount).IsEqualTo(expected: 10.25m);
		await Assert.That(value: money.Currency.Value).IsEqualTo(expected: "EUR");
	}

	[Test]
	public async Task Read_WithPropertiesInReverseOrder_ShouldStillReconstituteCorrectly()
	{
		Money money = JsonSerializer.Deserialize<Money>(json: """{"Currency":"JPY","Amount":100}""", options: Options);

		await Assert.That(value: money.Amount).IsEqualTo(expected: 100m);
		await Assert.That(value: money.Currency.Value).IsEqualTo(expected: "JPY");
	}

	[Test]
	public async Task Read_WithUnknownProperty_ShouldIgnoreItAndStillReconstituteKnownFields()
	{
		Money money = JsonSerializer.Deserialize<Money>(json: """{"SomeExtraField":"ignored","Amount":5,"Currency":"GBP"}""", options: Options);

		await Assert.That(value: money.Amount).IsEqualTo(expected: 5m);
		await Assert.That(value: money.Currency.Value).IsEqualTo(expected: "GBP");
	}

	[Test]
	public async Task Read_WithoutAmountOrCurrency_ShouldDefaultToZeroAndDefaultCurrency()
	{
		Money money = JsonSerializer.Deserialize<Money>(json: "{}", options: Options);

		await Assert.That(value: money.Amount).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task Read_WithNonObjectToken_ShouldThrowJsonException()
	{
		await Assert.That(action: () => JsonSerializer.Deserialize<Money>(json: "\"not an object\"", options: Options)).Throws<JsonException>();
	}

	[Test]
	public async Task Read_ThenWrite_ShouldRoundTrip()
	{
		Money original = Money.Reconstitute(amount: 99.99m, currency: Currency.Reconstitute(value: "USD"));

		string json = JsonSerializer.Serialize(value: original, options: Options);
		Money roundTripped = JsonSerializer.Deserialize<Money>(json: json, options: Options);

		await Assert.That(value: roundTripped.Amount).IsEqualTo(expected: original.Amount);
		await Assert.That(value: roundTripped.Currency.Value).IsEqualTo(expected: original.Currency.Value);
	}
}
