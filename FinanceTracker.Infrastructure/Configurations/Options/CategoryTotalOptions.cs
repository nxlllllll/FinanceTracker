using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class CategoryTotalOptions
{
	public const string SectionName = "CategoryTotals";

	/// <summary>
	/// Number of transactions read per round-trip while rebuilding totals. Default: 1000.
	/// </summary>
	[Range(minimum: 100, maximum: 10_000)]
	public int RecalculationBatchSize { get; init; } = 1_000;
}
