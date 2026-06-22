using System.Reflection;
using DbUp;
using DbUp.Engine;

namespace FinanceTracker.Migrator;

/// <summary>Builds and runs the project's real DbUp migrations against a target database</summary>
public static class DatabaseMigrator
{
	/// <summary>Builds the upgrade engine without running it — lets callers inspect pending scripts first.</summary>
	public static UpgradeEngine CreateUpgradeEngine(string connectionString, bool logToConsole = true)
	{
		EnsureDatabase.For.PostgresqlDatabase(connectionString: connectionString);

		return logToConsole
			? DeployChanges.To.PostgresqlDatabase(connectionString: connectionString)
				.WithScriptsEmbeddedInAssembly(assembly: Assembly.GetExecutingAssembly())
				.WithTransactionPerScript()
				.LogToConsole()
				.Build()
			: DeployChanges.To.PostgresqlDatabase(connectionString: connectionString)
				.WithScriptsEmbeddedInAssembly(assembly: Assembly.GetExecutingAssembly())
				.WithTransactionPerScript()
				.Build();
	}

	/// <summary>Ensures the database exists and applies any pending migrations, throwing if any script fails.</summary>
	public static void Upgrade(string connectionString, bool logToConsole = true)
	{
		UpgradeEngine upgrader = CreateUpgradeEngine(connectionString: connectionString, logToConsole: logToConsole);
		DatabaseUpgradeResult result = upgrader.PerformUpgrade();

		if (!result.Successful)
			throw new InvalidOperationException(message: $"Database migration failed: {result.Error.Message}", innerException: result.Error);
	}
}