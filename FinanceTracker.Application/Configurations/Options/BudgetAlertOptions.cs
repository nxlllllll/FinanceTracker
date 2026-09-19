namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// Spending levels, in percent of a budget's limit, at which its owner is notified.
/// Bind from <c>appsettings.json</c> under the <c>"BudgetAlerts"</c> section.
/// </summary>
public sealed class BudgetAlertOptions
{
	public const string SectionName = "BudgetAlerts";

	/// <summary>Used when the section lists no thresholds.</summary>
	public static readonly int[] DefaultThresholds = [80, 100];

	/// <summary>Each value crossed upwards sends one notification.</summary>
	public int[] Thresholds { get; set; } = [];
}
