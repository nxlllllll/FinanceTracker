using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// Upper bound applied to every user-supplied monetary amount across commands — transaction,
/// transfer, recurring transaction, budget amounts, and the initial account balance.
/// Bind from <c>appsettings.json</c> under the <c>"MoneyLimits"</c> section.
/// </summary>
public sealed class MoneyLimitsOptions
{
	public const string SectionName = "MoneyLimits";

	/// <summary>Maximum allowed value for any single user-supplied monetary amount. Default: 999,999,999.99.</summary>
	[Range(type: typeof(decimal), minimum: "0.01", maximum: "79228162514264337593543950335", ParseLimitsInInvariantCulture = true)]
	public decimal MaxAmount { get; init; } = 999_999_999.99m;
}