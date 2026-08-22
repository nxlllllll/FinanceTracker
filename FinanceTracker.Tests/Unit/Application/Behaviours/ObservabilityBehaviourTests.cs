using FinanceTracker.Application.Behaviours.Tracing;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using UnitType = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

[NotInParallel]
public sealed class ObservabilityBehaviourTests
{
	private const string Executed = "command.executed";
	private const string Duration = "command.duration";

	public sealed record TestCommand : IRequest<Result<UnitType, AppException>>;

	private static ObservabilityBehaviour<TestCommand, Result<UnitType, AppException>> CreateBehaviour()
		=> new ObservabilityBehaviour<TestCommand, Result<UnitType, AppException>>();

	private static RequestHandlerDelegate<Result<UnitType, AppException>> Returns(Result<UnitType, AppException> result)
	{
		RequestHandlerDelegate<Result<UnitType, AppException>> next = Substitute.For<RequestHandlerDelegate<Result<UnitType, AppException>>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: result);
		return next;
	}

	[Test]
	public async Task Handle_WhenTheHandlerSucceeds_ShouldCountASuccess()
	{
		using MetricCollector collector = new MetricCollector(Executed, Duration);

		await CreateBehaviour().Handle(
			request: new TestCommand(),
			next: Returns(result: Result<UnitType, AppException>.Success(value: UnitType.Default)),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: collector.Total(
			instrument: Executed,
			(FinanceTrackerMetrics.Tags.RequestType, nameof(TestCommand)),
			(FinanceTrackerMetrics.Tags.Outcome, FinanceTrackerMetrics.CommandOutcomes.Success)
		)).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_WhenTheDomainRefuses_ShouldCountAFailureRatherThanAnError()
	{
		using MetricCollector collector = new MetricCollector(Executed);

		await CreateBehaviour().Handle(
			request: new TestCommand(),
			next: Returns(result: Result<UnitType, AppException>.Failure(error: new InsufficientFundsException(
				message: "Not enough money.",
				balance: Money.Create(amount: 0, currency: Currency.Reconstitute("RUB")).Value
			))),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: collector.Total(
			instrument: Executed,
			(FinanceTrackerMetrics.Tags.Outcome, FinanceTrackerMetrics.CommandOutcomes.Failure)
		)).IsEqualTo(expected: 1).Because(message: """
			The handler returned normally and the domain said no. Counting that as an error puts
			ordinary business rules on the same chart as crashes, and the chart stops meaning anything.
		""");

		await Assert.That(value: collector.Total(
			instrument: Executed,
			(FinanceTrackerMetrics.Tags.Outcome, FinanceTrackerMetrics.CommandOutcomes.Error)
		)).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Handle_WhenTheHandlerThrows_ShouldCountAnErrorAndRethrow()
	{
		using MetricCollector collector = new MetricCollector(Executed);

		RequestHandlerDelegate<Result<UnitType, AppException>> next = Substitute.For<RequestHandlerDelegate<Result<UnitType, AppException>>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => new InvalidOperationException("boom"));

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await CreateBehaviour().Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await Assert.That(value: collector.Total(
			instrument: Executed,
			(FinanceTrackerMetrics.Tags.Outcome, FinanceTrackerMetrics.CommandOutcomes.Error)
		)).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_ShouldRecordDurationOnBothPaths()
	{
		using MetricCollector collector = new MetricCollector(Duration);

		await CreateBehaviour().Handle(
			request: new TestCommand(),
			next: Returns(result: Result<UnitType, AppException>.Success(value: UnitType.Default)),
			cancellationToken: CancellationToken.None
		);

		RequestHandlerDelegate<Result<UnitType, AppException>> throwing = Substitute.For<RequestHandlerDelegate<Result<UnitType, AppException>>>();
		throwing(t: Arg.Any<CancellationToken>()).Throws(createException: _ => new InvalidOperationException("boom"));

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await CreateBehaviour().Handle(
			request: new TestCommand(),
			next: throwing,
			cancellationToken: CancellationToken.None
		));

		await Assert.That(value: collector.For(instrument: Duration).Count).IsEqualTo(expected: 2).Because(message: """
			A request that fails still took time, and often more of it than one that succeeded.
			Recording only the happy path makes the latency chart look best exactly when things are
			going worst.
		""");
	}
}
