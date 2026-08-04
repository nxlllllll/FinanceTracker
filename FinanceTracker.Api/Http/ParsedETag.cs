namespace FinanceTracker.Api.Http;

/// <summary>Outcome of parsing an <c>If-Match</c> header.</summary>
public readonly record struct ParsedETag(bool IsPresent, bool IsValid, bool IsWildcard, int? Version)
{
	public static ParsedETag Absent => new ParsedETag(IsPresent: false, IsValid: true, IsWildcard: false, Version: null);
	public static ParsedETag Invalid => new ParsedETag(IsPresent: true, IsValid: false, IsWildcard: false, Version: null);
	public static ParsedETag MatchesAny => new ParsedETag(IsPresent: true, IsValid: true, IsWildcard: true, Version: null);

	public static ParsedETag ForVersion(int version) => new ParsedETag(IsPresent: true, IsValid: true, IsWildcard: false, Version: version);
}
