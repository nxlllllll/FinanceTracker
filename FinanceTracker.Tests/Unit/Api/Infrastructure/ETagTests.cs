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
[Test]
	public async Task Parse_WithNoHeader_ShouldReportAbsentRatherThanInvalid()
	{
		ParsedETag parsed = ETag.Parse(ifMatchHeaderValue: null);

		await Assert.That(value: parsed.IsPresent).IsFalse();
		await Assert.That(value: parsed.IsValid).IsTrue().Because(message: """
			Sending no precondition is a legitimate request, not a malformed one. Collapsing the two would
			make every unconditional write fail.
		""");
		await Assert.That(value: parsed.Version).IsNull();
	}

	[Test]
	public async Task Parse_WithStrongTag_ShouldReadTheVersion()
	{
		ParsedETag parsed = ETag.Parse(ifMatchHeaderValue: ETag.FromVersion(version: 7));

		await Assert.That(value: parsed.IsValid).IsTrue();
		await Assert.That(value: parsed.Version).IsEqualTo(expected: 7);
	}

	[Test]
	public async Task Parse_WithWildcard_ShouldMatchAnyVersion()
	{
		ParsedETag parsed = ETag.Parse(ifMatchHeaderValue: ETag.Wildcard);

		await Assert.That(value: parsed.IsValid).IsTrue();
		await Assert.That(value: parsed.IsWildcard).IsTrue();
		await Assert.That(value: parsed.Version).IsNull().Because(message: """
			A wildcard means "whatever version is current", so there is no number to compare against and
			the write proceeds unconditionally.
		""");
	}

	[Test]
	[Arguments("\"not-a-number\"")]
	[Arguments("garbage")]
	[Arguments("\"1\", \"2\"")]
	[Arguments("W/\"7\"")]
	public async Task Parse_WithUnusableValue_ShouldReportInvalid(string headerValue)
	{
		ParsedETag parsed = ETag.Parse(ifMatchHeaderValue: headerValue);

		await Assert.That(value: parsed.IsPresent).IsTrue();
		await Assert.That(value: parsed.IsValid).IsFalse().Because(message: """
			The client asked for a precondition this API cannot evaluate. Treating that as "no
			precondition" silently disables the very check the header exists for.
		""");
	}

	[Test]
	public async Task Parse_ThenToVersion_ShouldAgreeOnTheVersion()
	{
		string formatted = ETag.FromVersion(version: 42);

		await Assert.That(value: ETag.Parse(ifMatchHeaderValue: formatted).Version)
			.IsEqualTo(expected: ETag.ToVersion(ifMatchHeaderValue: formatted));
	}
}
