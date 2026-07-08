using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.Converters.Json;

public sealed class NameJsonConverterTests
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { Converters = { new NameJsonConverter() } };

	[Test]
	public async Task Write_ShouldWriteRawStringValue()
	{
		Name name = Name.Reconstitute(value: "Main Account");

		string json = JsonSerializer.Serialize(value: name, options: Options);

		await Assert.That(value: json).IsEqualTo(expected: "\"Main Account\"");
	}

	[Test]
	public async Task Read_ShouldReconstituteNameFromString()
	{
		Name name = JsonSerializer.Deserialize<Name>(json: "\"Savings\"", options: Options);

		await Assert.That(value: name.Value).IsEqualTo(expected: "Savings");
	}

	[Test]
	public async Task Read_WithNullToken_ShouldThrowJsonException()
	{
		await Assert.That(action: () => JsonSerializer.Deserialize<Name>(json: "null", options: Options)).Throws<JsonException>();
	}

	[Test]
	public async Task Read_ThenWrite_ShouldRoundTrip()
	{
		Name original = Name.Reconstitute(value: "Round Trip");

		string json = JsonSerializer.Serialize(value: original, options: Options);
		Name roundTripped = JsonSerializer.Deserialize<Name>(json: json, options: Options);

		await Assert.That(value: roundTripped).IsEqualTo(expected: original);
	}
}
