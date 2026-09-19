using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// Refuses to start on a <see cref="BudgetAlertOptions"/> section that would notify nobody,
/// or notify about a level a budget cannot cross.
/// </summary>
public sealed class BudgetAlertOptionsValidator : IValidateOptions<BudgetAlertOptions>
{
	private const int MinThreshold = 1;
	private const int MaxThreshold = 100;

	public ValidateOptionsResult Validate(string? name, BudgetAlertOptions options)
	{
		string thresholds = $"{BudgetAlertOptions.SectionName}:{nameof(BudgetAlertOptions.Thresholds)}";

		List<string> failures = [];

		if (options.Thresholds.Length == 0)
			failures.Add(item: $"{thresholds} must list at least one threshold.");

		foreach (int threshold in options.Thresholds.Where(predicate: threshold => threshold is < MinThreshold or > MaxThreshold))
			failures.Add(item: $"{thresholds} must be between {MinThreshold} and {MaxThreshold} — {threshold} is not.");

		if (options.Thresholds.Distinct().Count() != options.Thresholds.Length)
			failures.Add(item: $"{thresholds} lists the same threshold more than once.");

		if (failures.Count == 0)
			return ValidateOptionsResult.Success;

		return ValidateOptionsResult.Fail(failures: failures);
	}
}
