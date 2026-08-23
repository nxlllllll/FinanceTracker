using System.Text.RegularExpressions;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinanceTracker.Tests.Architecture;

public sealed partial class ColumnNamingArchitectureTests
{
	private static readonly Dictionary<string, string> KnownExceptions = new Dictionary<string, string>(comparer: StringComparer.Ordinal)
	{
		["AccountEntity.AccountType"] = "account_type_code",
		["AccountEntity.Currency"] = "currency_code",
		["BudgetEntity.From"] = "date_from",
		["BudgetEntity.To"] = "date_to",
		["BudgetEntity.Currency"] = "currency_code",
		["CategoryEntity.Type"] = "type_code",
		["RecurringTransactionEntity.Currency"] = "currency_code",
		["RecurringTransactionEntity.Direction"] = "direction_type",
		["TransactionEntity.Currency"] = "currency_code",
		["TransactionEntity.BaseCurrency"] = "base_currency_code",
		["TransactionEntity.Direction"] = "direction_type",
		["UnresolvableEventEntity.Type"] = "type_code"
	};

	[GeneratedRegex(pattern: "(?<!^)([A-Z])")]
	private static partial Regex ColumnRegex();

	private static IModel BuildModel()
	{
		DbContextOptions<FinanceTrackerContext> options = new DbContextOptionsBuilder<FinanceTrackerContext>()
			.UseNpgsql(connectionString: "Host=localhost;Database=model-only;Username=none;Password=none")
			.Options;

		using FinanceTrackerContext context = new FinanceTrackerContext(options: options);

		return context.Model;
	}

	private static string ExpectedColumnName(string propertyName)
		=> ColumnRegex().Replace(input: propertyName, replacement: "_$1").ToLowerInvariant();

	private static IEnumerable<(string Key, string Property, string Actual)> AllColumns()
	{
		foreach (IEntityType entityType in BuildModel().GetEntityTypes())
		{
			foreach (IProperty property in entityType.GetProperties())
			{
				string key = $"{entityType.ClrType.Name}.{property.Name}";

				yield return (key, property.Name, property.GetColumnName());
			}
		}
	}

	[Test]
	public async Task EveryColumnName_ShouldFollowFromItsPropertyName()
	{
		List<string> unexpected = [];

		foreach ((string key, string propertyName, string actual) in AllColumns())
		{
			if (KnownExceptions.ContainsKey(key: key))
				continue;

			string expected = ExpectedColumnName(propertyName: propertyName);

			if (actual != expected)
				unexpected.Add(item: $"{key}: '{actual}', expected '{expected}'");
		}

		await Assert.That(value: unexpected).IsEmpty().Because(message: $"""
			{unexpected.Count} column(s) deviate without being listed as exceptions:

			{String.Join(separator: Environment.NewLine, values: unexpected)}

			Either the name genuinely has to differ — add it to KnownExceptions with the reason — or an
			explicit HasColumnName was removed from a column the convention cannot reproduce, and the
			schema and the model have just stopped agreeing.
		""");
	}

	[Test]
	public async Task EveryListedException_ShouldStillDeviate()
	{
		Dictionary<string, string> actualByKey = AllColumns().ToDictionary(
			keySelector: column => column.Key,
			elementSelector: column => column.Actual,
			comparer: StringComparer.Ordinal
		);

		List<string> stale = [];

		foreach ((string key, string expectedColumn) in KnownExceptions)
		{
			if (!actualByKey.TryGetValue(key: key, value: out string? actual))
			{
				stale.Add(item: $"{key}: no such property — renamed or removed");
				continue;
			}

			if (actual != expectedColumn)
			{
				stale.Add(item: $"{key}: listed as '{expectedColumn}' but resolved to '{actual}'");
				continue;
			}

			string derivable = ExpectedColumnName(propertyName: key.Split(separator: '.')[1]);

			if (actual == derivable)
				stale.Add(item: $"{key}: '{actual}' now follows from the property and no longer needs an exception");
		}

		await Assert.That(value: stale).IsEmpty().Because(message: $"""
			The exception list has drifted from the model:

			{String.Join(separator: Environment.NewLine, values: stale)}

			A list of exceptions is only useful while every entry is still an exception. Entries that no
			longer apply teach the next reader that a rule has more holes than it does.
		""");
	}
}
