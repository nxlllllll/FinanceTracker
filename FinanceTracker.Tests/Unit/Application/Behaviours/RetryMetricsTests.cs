using FinanceTracker.Application.Behaviours.Retry;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.Metrics;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

[NotInParallel]
public sealed class RetryMetricsTests
{
	private const string Retried = "command.retried";

	public sealed record TestCommand : IRequest<TestResponse>;

	public sealed record TestResponse;

	private static RetryBehaviour<TestCommand, TestResponse> CreateBehaviour(bool transientFaults)
	{
		IOptionsMonitor<RetryOptions> options = new FakeOptionsMonitor<RetryOptions>(value: new RetryOptions
		{
			MaxRetries = 3,
			BaseDelayMs = 0,
			UseJitter = false
		});

		ITransientFaultDetector detector = Substitute.For<ITransientFaultDetector>();
		detector.IsTransient(exception: Arg.Any<Exception>()).Returns(returnThis: transientFaults);

		return new RetryBehaviour<TestCommand, TestResponse>(
			logger: Substitute.For<ILogger<RetryBehaviour<TestCommand, TestResponse>>>(),
			options: options,
			transientFaultDetector: detector
		);
	}

	private static RequestHandlerDelegate<TestResponse> FailsOnceThenSucceeds(Exception exception)
	{
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 1)
				throw exception;
			return Task.FromResult(result: new TestResponse());
		});

		return next;
	}

	[Test]
	public async Task Handle_OnAVersionConflict_ShouldTagTheRetryAsConcurrency()
	{
		using MetricCollector collector = new MetricCollector(Retried);

		await CreateBehaviour(transientFaults: false).Handle(
			request: new TestCommand(),
			next: FailsOnceThenSucceeds(exception: new ConcurrencyConflictException(message: "conflict", id: Guid.CreateVersion7())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: collector.Total(
			instrument: Retried,
			(FinanceTrackerMetrics.Tags.Reason, FinanceTrackerMetrics.RetryReasons.ConcurrencyConflict)
		)).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_OnATransientFault_ShouldTagTheRetryAsTransient()
	{
		using MetricCollector collector = new MetricCollector(Retried);

		await CreateBehaviour(transientFaults: true).Handle(
			request: new TestCommand(),
			next: FailsOnceThenSucceeds(exception: new InvalidOperationException("connection reset")),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: collector.Total(
			instrument: Retried,
			(FinanceTrackerMetrics.Tags.Reason, FinanceTrackerMetrics.RetryReasons.TransientFault)
		)).IsEqualTo(expected: 1);

		await Assert.That(value: collector.Total(
			instrument: Retried,
			(FinanceTrackerMetrics.Tags.Reason, FinanceTrackerMetrics.RetryReasons.ConcurrencyConflict)
		)).IsEqualTo(expected: 0).Because(message: "A dropped connection is not contention, and a chart that shows it as such sends the reader looking in the wrong place.");
	}

	[Test]
	public async Task Handle_WhenEveryAttemptFails_ShouldCountEachRetryRatherThanTheFailure()
	{
		const int maxRetries = 3;

		using MetricCollector collector = new MetricCollector(Retried);

		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => new ConcurrencyConflictException(message: "conflict", id: Guid.CreateVersion7()));

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await CreateBehaviour(transientFaults: false).Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await Assert.That(value: collector.Total(instrument: Retried)).IsEqualTo(expected: maxRetries).Because(message: """
			This counts attempts, not outcomes — how much retrying the system is doing. Final failures
			are already visible as command.executed with outcome=error, and counting them twice under
			different names would make both numbers harder to trust.
		""");
	}

	[Test]
	public async Task Handle_WhenNothingFails_ShouldCountNothing()
	{
		using MetricCollector collector = new MetricCollector(Retried);

		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: new TestResponse());

		await CreateBehaviour(transientFaults: false).Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: collector.Total(instrument: Retried)).IsEqualTo(expected: 0);
	}
}
