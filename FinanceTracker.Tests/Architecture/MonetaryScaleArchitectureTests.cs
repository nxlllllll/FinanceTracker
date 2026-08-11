using FinanceTracker.Tests.Architecture.Helpers;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Keeps the monetary scale rule from being forgotten in a validator written six months from now.
/// </summary>
public sealed class MonetaryScaleArchitectureTests
{
	private const string ApplicationProject = "FinanceTracker.Application";
	private const string MoneyLimitsMarker = "MoneyLimitsOptions";
	private const string ScaleRuleMarker = "MustFitAmountScale";

	/// <summary>
	/// Validators that price something in money, keyed by file name for readable failures.
	/// </summary>
	private static Dictionary<string, string> MoneyValidatorSources()
	{
		return SourceScan.FilesIn(projectName: ApplicationProject)
			.Where(predicate: path => Path.GetFileName(path: path).EndsWith(value: "Validator.cs", comparisonType: StringComparison.Ordinal))
			.Select(selector: path => (Name: Path.GetFileName(path: path), Source: SourceScan.StripComments(source: File.ReadAllText(path: path))))
			.Where(predicate: file => file.Source.Contains(value: MoneyLimitsMarker, comparisonType: StringComparison.Ordinal))
			.ToDictionary(keySelector: file => file.Name, elementSelector: file => file.Source);
	}

	[Test]
	public async Task MoneyValidatorScan_ShouldFindValidatorsToCheck()
	{
		Dictionary<string, string> validators = MoneyValidatorSources();

		await Assert.That(value: validators).IsNotEmpty();
	}

	[Test]
	public async Task EveryValidatorConstrainingAmountRange_ShouldAlsoConstrainScale()
	{
		Dictionary<string, string> validators = MoneyValidatorSources();

		List<string> missing = validators.Where(predicate: file => !file.Value.Contains(value: ScaleRuleMarker, comparisonType: StringComparison.Ordinal))
			.Select(selector: file => file.Key)
			.OrderBy(keySelector: name => name)
			.ToList();

		await Assert.That(value: missing).IsEmpty().Because(message: $"""
			These validators bound an amount's range but not its number of decimal places, so a value
			like 100.123456 passes and PostgreSQL rounds it into numeric(18, 2) on write. The response
			still echoes what the caller sent, so the two only disagree on the next read. Add
			.{ScaleRuleMarker}() to the amount rule: {String.Join(separator: ", ", values: missing)}
		""");
	}

	[Test]
	public async Task ExchangeRateIngestion_ShouldNotBypassRateValidation()
	{
		string source = SourceScan.StripComments(
			source: SourceScan.ReadFile(projectName: "FinanceTracker.Worker.CurrencyRate", pathSegments: ["Job", "CurrencyRateJob.cs"])
		);

		bool usesReconstitute = source.Contains(value: "CurrencyRate.Reconstitute", comparisonType: StringComparison.Ordinal);

		await Assert.That(value: usesReconstitute).IsFalse().Because(message:
			"CurrencyRateJob ingests rates from an external provider and must go through CurrencyRate.Create, " +
			"which rejects unusable values and normalises precision to what numeric(18, 6) keeps."
		);
	}
}
