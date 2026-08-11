using FinanceTracker.Core.Persistence;
using Npgsql;

namespace FinanceTracker.Infrastructure.Database.Resilience;

/// <summary>
/// Recognises PostgreSQL failures that a second attempt could get past.
/// </summary>
public sealed class NpgsqlTransientFaultDetector : ITransientFaultDetector
{
	/// <summary>
	/// Failures Postgres itself describes as worth repeating. Everything else is either a decision
	/// that will be the same next time, or a bug.
	/// </summary>
	private static readonly HashSet<string> RetryableSqlStates =
	[
		// The transaction could not be serialised against a concurrent one. Postgres asks the client
		// to retry; this is the documented way to handle it.
		"40001",

		// Deadlock. One side was chosen as the victim and rolled back, and the ordering that caused
		// it is unlikely to repeat.
		"40P01",

		// A lock could not be taken within the timeout. Whoever held it has probably let go by now.
		"55P03",

		// The server is shutting down. The next attempt reaches a different instance.
		"57P01",

		// Connection lost while the query was running.
		"08006",
		"08003",
		"08000"
	];

	public bool IsTransient(Exception exception)
	{
		if (exception is OperationCanceledException)
			return false;

		return exception switch
		{
			PostgresException postgres => RetryableSqlStates.Contains(item: postgres.SqlState),
			NpgsqlException npgsql => npgsql.IsTransient,
			{ InnerException: not null } => IsTransient(exception: exception.InnerException),
			_ => false
		};
	}
}
