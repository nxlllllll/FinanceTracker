namespace FinanceTracker.Api.Infrastructure;

public static class ETag
{
	public static string FromVersion(int version) => $"\"{version}\"";

	public static int? ToVersion(string? ifMatchHeaderValue)
	{
		if (String.IsNullOrWhiteSpace(value: ifMatchHeaderValue))
			return null;

		string trimmed = ifMatchHeaderValue.Trim().Trim(trimChar: '"');
		if (Int32.TryParse(s: trimmed, result: out int version))
			return version;

		return null;
	}
}
