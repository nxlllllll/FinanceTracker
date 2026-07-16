using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Core.Converters.Json;

public sealed class EmailJsonConverterTests
{
	private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { Converters = { new EmailJsonConverter() } };

	[Test]
	public async Task Write_ShouldWriteRawStringValue()
	{
		Email email = Email.Reconstitute(value: "user@example.com");

		string json = JsonSerializer.Serialize(value: email, options: Options);

		await Assert.That(value: json).IsEqualTo(expected: "\"user@example.com\"");
	}

	[Test]
	public async Task Read_ShouldReconstituteEmailFromString()
	{
		Email email = JsonSerializer.Deserialize<Email>(json: "\"someone@test.com\"", options: Options);

		await Assert.That(value: email.Value).IsEqualTo(expected: "someone@test.com");
	}

	[Test]
	public async Task Read_WithNullToken_ShouldThrowJsonException()
	{
		await Assert.That(action: () => JsonSerializer.Deserialize<Email>(json: "null", options: Options)).Throws<JsonException>();
	}

	[Test]
	public async Task Read_ThenWrite_ShouldRoundTrip()
	{
		Email original = Email.Reconstitute(value: "round@trip.com");

		string json = JsonSerializer.Serialize(value: original, options: Options);
		Email roundTripped = JsonSerializer.Deserialize<Email>(json: json, options: Options);

		await Assert.That(value: roundTripped).IsEqualTo(expected: original);
	}
}
