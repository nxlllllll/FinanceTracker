using FinanceTracker.Application.Behaviours.Retry;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class ConcurrencyRetryBehaviourTests
{
	public sealed record TestCommand : IRequest<TestResponse>;
	public sealed class TestResponse;

	private static RetryBehaviour<TestCommand, TestResponse> CreateBehavior(
		int maxRetries = 3,
		int baseDelayMs = 0,
		bool useJitter = false,
		bool transientFaults = false)
	{
		IOptionsMonitor<RetryOptions> options = new FakeOptionsMonitor<RetryOptions>(value: new RetryOptions
		{
			MaxRetries = maxRetries,
			BaseDelayMs = baseDelayMs,
			UseJitter = useJitter
		});

		ITransientFaultDetector detector = Substitute.For<ITransientFaultDetector>();
		detector.IsTransient(exception: Arg.Any<Exception>()).Returns(returnThis: transientFaults);

		return new RetryBehaviour<TestCommand, TestResponse>(
			logger: Substitute.For<ILogger<RetryBehaviour<TestCommand, TestResponse>>>(),
			options: options,
			transientFaultDetector: detector
		);
	}

	private static ConcurrencyConflictException MakeConflict()
		=> new ConcurrencyConflictException(message: "Conflict.", id: Guid.NewGuid());

	[Test]
	public async Task Handle_WhenNoException_ShouldCallNextOnce()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ => new TestResponse());

		await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenNoException_ShouldReturnResult()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior();
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ => expected);

		TestResponse result = await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task Handle_WhenFirstAttemptFails_ShouldRetryAndSucceed()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3);
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 1)
				throw MakeConflict();
			return Task.FromResult(result: expected);
		});

		TestResponse result = await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: expected);
		await next.Received(requiredNumberOfCalls: 2).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenAllRetriesFail_ShouldThrowConcurrencyConflictException()
	{
		const int maxRetries = 2;
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeConflict());

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: maxRetries + 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenSucceedsOnLastRetry_ShouldReturnResult()
	{
		const int maxRetries = 3;
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries);
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount <= maxRetries)
				throw MakeConflict();
			return Task.FromResult(result: expected);
		});

		TestResponse result = await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: expected);
		await next.Received(requiredNumberOfCalls: maxRetries + 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenOtherExceptionThrown_ShouldNotRetry()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => new InvalidOperationException("unrelated"));

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenCancelledDuringDelay_ShouldThrowOperationCancelled()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 5, baseDelayMs: 5000, useJitter: false);

		using CancellationTokenSource cts = new CancellationTokenSource();

		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns<TestResponse>(returnThis: _ =>
		{
			cts.Cancel();
			throw MakeConflict();
		});

		await Assert.ThrowsAsync<OperationCanceledException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: cts.Token
		));
	}

	[Test]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(5)]
	public async Task Handle_WithCustomMaxRetries_ShouldCallExactlyMaxRetriesPlusOneTimesOnFailure(int maxRetries)
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeConflict());

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: maxRetries + 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	public async Task CalculateDelay_ShouldNotOverflowAndReturnNonNegativeValue(int attempt)
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 10, baseDelayMs: 10, useJitter: false);

		int callCount = 0;
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			if (callCount++ <= attempt)
				throw MakeConflict();
			return Task.FromResult(result: new TestResponse());
		});

		await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: attempt + 2).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WithJitterEnabled_ShouldNotThrowAndReturnResult()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3, baseDelayMs: 0, useJitter: true);

		TestResponse expected = new TestResponse();
		int callCount = 0;
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			if (callCount++ == 0)
				throw MakeConflict();
			return Task.FromResult(result: expected);
		});

		TestResponse result = await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task Handle_WithZeroMaxRetries_ShouldNotRetry()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 0);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeConflict());

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenTransientFaultThrown_ShouldRetryAndSucceed()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3, transientFaults: true);
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 1)
				throw new InvalidOperationException("connection reset");
			return Task.FromResult(result: expected);
		});

		TestResponse result = await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: expected);
		await next.Received(requiredNumberOfCalls: 2).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenAllRetriesFailOnTransientFault_ShouldThrowOriginalException()
	{
		const int maxRetries = 2;
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries, transientFaults: true);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => new InvalidOperationException("connection reset"));

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: maxRetries + 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenDetectorRejectsTheFault_ShouldStillRetryConcurrencyConflicts()
	{
		RetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3, transientFaults: false);
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 1)
				throw MakeConflict();
			return Task.FromResult(result: expected);
		});

		await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 2).Invoke(t: Arg.Any<CancellationToken>());
	}
}
