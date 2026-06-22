using DbUp.Engine;
using Microsoft.Extensions.Configuration;

namespace FinanceTracker.Migrator;

public sealed class Program
{
	private static int ColorPrinting(ConsoleColor color, string text, int exitCode)
	{
		Console.ForegroundColor = color;
		Console.WriteLine(value: text);
		Console.ResetColor();
		return exitCode;
	}
	
	public static int Main(string[] args)
	{
		IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(path: "appsettings.json", optional: false)
			.AddJsonFile(path: "appsettings.Development.json", optional: false)
			.AddEnvironmentVariables()
			.Build();

		string connectionString = configuration.GetConnectionString(name: "FinanceTrackerContext")
			?? throw new InvalidOperationException(message: "Connection string 'FinanceTrackerContext' is not configured.");

		UpgradeEngine upgrader = DatabaseMigrator.CreateUpgradeEngine(connectionString: connectionString);

		string? lastApplied = upgrader.GetExecutedScripts().LastOrDefault();

		if (lastApplied is not null)
			Console.WriteLine(value: $"[Migrator] Last applied: {lastApplied}");
		else
			Console.WriteLine(value: "[Migrator] No migrations applied yet.");

		IReadOnlyList<SqlScript> pending = upgrader.GetScriptsToExecute();

		if (pending.Count == 0)
			return ColorPrinting(color: ConsoleColor.Green, text: "[Migrator] Database is up to date. Nothing to apply.", exitCode: 0);

		Console.WriteLine(value: $"[Migrator] Pending migrations ({pending.Count}):");
		foreach (SqlScript script in pending)
			Console.WriteLine(value: $"  > {script.Name}");

		DatabaseUpgradeResult result = upgrader.PerformUpgrade();

		if (!result.Successful)
			return ColorPrinting(color: ConsoleColor.Red, text: $"[Migrator] Failed: {result.Error.Message}", exitCode: 1);
		
		return ColorPrinting(color: ConsoleColor.Green, text: $"[Migrator] All migrations applied successfully.", exitCode: 0);
	}
}