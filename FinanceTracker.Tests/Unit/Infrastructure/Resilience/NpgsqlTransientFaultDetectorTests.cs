using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Infrastructure.Database.Resilience;
using Npgsql;

namespace FinanceTracker.Tests.Unit.Infrastructure.Resilience;

public sealed class NpgsqlTransientFaultDetectorTests
{
	private readonly NpgsqlTransientFaultDetector _detector = new NpgsqlTransientFaultDetector();

	private static PostgresException WithSqlState(string sqlState) => new PostgresException(
		messageText: "test",
		severity: "ERROR",
		invariantSeverity: "ERROR",
		sqlState: sqlState
	);

	[Test]
	[Arguments("40001")]
	[Arguments("40P01")]
	[Arguments("55P03")]
	[Arguments("57P01")]
	public async Task SqlStatesPostgresAsksClientsToRetry_ShouldBeTransient(string sqlState)
		=> await Assert.That(value: _detector.IsTransient(exception: WithSqlState(sqlState: sqlState))).IsTrue();

	[Test]
	[Arguments("23505")] // unique_violation
	[Arguments("23503")] // foreign_key_violation
	[Arguments("23514")] // check_violation
	[Arguments("42601")] // syntax_error
	[Arguments("42501")] // insufficient_privilege
	public async Task DecisionsThatWillNotChange_ShouldNotBeTransient(string sqlState)
	{
		await Assert.That(value: _detector.IsTransient(exception: WithSqlState(sqlState: sqlState))).IsFalse().Because(message: """
			A violated constraint or a missing permission is the same on the next attempt. Retrying
			only delays the answer, and burns the attempt budget that a genuinely transient failure
			would have needed.
		""");
	}

	[Test]
	public async Task Cancellation_ShouldNotBeTransient()
	{
		await Assert.That(value: _detector.IsTransient(exception: new OperationCanceledException())).IsFalse()
			.Because(message: "The caller has gone. Repeating the work spends time on a result nobody is waiting for.");
	}

	[Test]
	public async Task AWrappedTransientFault_ShouldStillBeRecognised()
	{
		Exception wrapped = new InvalidOperationException(
			message: "An exception occurred while saving.",
			innerException: WithSqlState(sqlState: "40001")
		);

		await Assert.That(value: _detector.IsTransient(exception: wrapped)).IsTrue().Because(message: """
			EF and the repositories wrap provider exceptions on the way out, so the outer type usually
			says nothing. Looking only at the top would make this detector answer no for almost every
			real failure.
		""");
	}

	[Test]
	public async Task AnUnrelatedException_ShouldNotBeTransient()
	{
		await Assert.That(value: _detector.IsTransient(exception: new InvalidOperationException(message: "something else"))).IsFalse();
		await Assert.That(value: _detector.IsTransient(exception: new ConcurrencyConflictException(message: "conflict", id: Guid.CreateVersion7()))).IsFalse()
			.Because(message: "Version conflicts are retried too, but by name in the behaviour — they are not a database fault, and this detector should not claim them.");
	}
}
