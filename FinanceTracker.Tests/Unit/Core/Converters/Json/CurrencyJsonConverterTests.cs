using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.Converters.Json;

public sealed class CurrencyJsonConverterTests
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { Converters = { new CurrencyJsonConverter() } };

	[Test]
	public async Task Write_ShouldWriteRawStringValue()
	{
		Currency currency = Currency.Reconstitute(value: "USD");

		string json = JsonSerializer.Serialize(value: currency, options: Options);

		await Assert.That(value: json).IsEqualTo(expected: "\"USD\"");
	}

	[Test]
	public async Task Read_ShouldReconstituteCurrencyFromString()
	{
		Currency currency = JsonSerializer.Deserialize<Currency>(json: "\"EUR\"", options: Options);

		await Assert.That(value: currency.Value).IsEqualTo(expected: "EUR");
	}

	[Test]
	public async Task Read_WithNullToken_ShouldThrowJsonException()
	{
		await Assert.That(action: () => JsonSerializer.Deserialize<Currency>(json: "null", options: Options)).Throws<JsonException>();
	}

	[Test]
	public async Task Read_ThenWrite_ShouldRoundTrip()
	{
		Currency original = Currency.Reconstitute(value: "GBP");

		string json = JsonSerializer.Serialize(value: original, options: Options);
		Currency roundTripped = JsonSerializer.Deserialize<Currency>(json: json, options: Options);

		await Assert.That(value: roundTripped).IsEqualTo(expected: original);
	}
}

