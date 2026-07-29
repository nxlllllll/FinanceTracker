using FinanceTracker.Api.Http;

namespace FinanceTracker.Tests.Unit.Api.Infrastructure;

public sealed class ETagTests
{
	[Test]
	public async Task FromVersion_ShouldWrapTheVersionInQuotes()
	{
		string result = ETag.FromVersion(version: 3);

		await Assert.That(value: result).IsEqualTo(expected: "\"3\"");
	}

	[Test]
	public async Task ToVersion_WithQuotedValue_ShouldParseIt()
	{
		int? result = ETag.ToVersion(ifMatchHeaderValue: "\"3\"");

		await Assert.That(value: result).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task ToVersion_WithUnquotedValue_ShouldStillParseIt()
	{
		int? result = ETag.ToVersion(ifMatchHeaderValue: "3");

		await Assert.That(value: result).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task ToVersion_WithNullValue_ShouldReturnNull()
	{
		int? result = ETag.ToVersion(ifMatchHeaderValue: null);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task ToVersion_WithEmptyValue_ShouldReturnNull()
	{
		int? result = ETag.ToVersion(ifMatchHeaderValue: "");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task ToVersion_WithWhitespaceValue_ShouldReturnNull()
	{
		int? result = ETag.ToVersion(ifMatchHeaderValue: "   ");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task ToVersion_WithNonNumericValue_ShouldReturnNull()
	{
		int? result = ETag.ToVersion(ifMatchHeaderValue: "\"not-a-number\"");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task FromVersion_ThenToVersion_ShouldRoundTrip()
	{
		string formatted = ETag.FromVersion(version: 42);
		int? parsed = ETag.ToVersion(ifMatchHeaderValue: formatted);

		await Assert.That(value: parsed).IsEqualTo(expected: 42);
	}
}
