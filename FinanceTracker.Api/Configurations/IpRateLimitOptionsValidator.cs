using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Configurations;

/// <summary>
/// Refuses to start on an <see cref="IpRateLimitOptions"/> section that would produce a limit
/// nobody intended.
/// </summary>
public sealed class IpRateLimitOptionsValidator : IValidateOptions<IpRateLimitOptions>
{
	public ValidateOptionsResult Validate(string? name, IpRateLimitOptions options)
	{
		List<string> failures = [];

		if (options.RequestsPerWindow <= 0)
		{
			failures.Add(item: $"""
				{IpRateLimitOptions.SectionName}:{nameof(IpRateLimitOptions.RequestsPerWindow)} must be greater than zero — it is {options.RequestsPerWindow},
				which would refuse every request from every address. Set {IpRateLimitOptions.SectionName}:{nameof(IpRateLimitOptions.Enabled)} to false to turn the limit off.
			""");
		}

		if (options.WindowSeconds <= 0)
			failures.Add(item: $"{IpRateLimitOptions.SectionName}:{nameof(IpRateLimitOptions.WindowSeconds)} must be greater than zero — it is {options.WindowSeconds}.");

		if (failures.Count == 0)
			return ValidateOptionsResult.Success;

		return ValidateOptionsResult.Fail(failures: failures);
	}
}
