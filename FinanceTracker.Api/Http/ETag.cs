namespace FinanceTracker.Api.Http;

public static class ETag
{
	/// <summary>Matches any current version — <c>If-Match: *</c>.</summary>
	public const string Wildcard = "*";

	public static string FromVersion(int version) => $"\"{version}\"";

	public static ParsedETag Parse(string? ifMatchHeaderValue)
	{
		if (String.IsNullOrWhiteSpace(value: ifMatchHeaderValue))
			return ParsedETag.Absent;

		string raw = ifMatchHeaderValue.Trim();

		if (raw == Wildcard)
			return ParsedETag.MatchesAny;

		raw = raw.Trim().Trim(trimChar: '"');

		if (Int32.TryParse(s: raw, result: out int version))
			return ParsedETag.ForVersion(version: version);

		return ParsedETag.Invalid;
	}

	public static int? ToVersion(string? ifMatchHeaderValue)
		=> Parse(ifMatchHeaderValue: ifMatchHeaderValue).Version;
}
