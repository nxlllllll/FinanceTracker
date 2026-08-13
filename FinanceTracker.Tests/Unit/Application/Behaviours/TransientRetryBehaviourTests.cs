using FinanceTracker.Application.Behaviours.Retry;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class TransientRetryBehaviourTests
{
	public sealed record TestCommand : IRequest<TestResponse>;
	public sealed class TestResponse;

	private static TransientRetryBehaviour<TestCommand, TestResponse> CreateBehavior(
		int maxRetries = 3,
		int baseDelayMs = 0,
		bool useJitter = false,
		bool transientFaults = true)
	{
		IOptionsMonitor<RetryOptions> options = new FakeOptionsMonitor<RetryOptions>(value: new RetryOptions
		{
			MaxRetries = maxRetries,
			BaseDelayMs = baseDelayMs,
			UseJitter = useJitter
		});

		ITransientFaultDetector detector = Substitute.For<ITransientFaultDetector>();
		detector.IsTransient(exception: Arg.Any<Exception>()).Returns(returnThis: transientFaults);

		return new TransientRetryBehaviour<TestCommand, TestResponse>(
			logger: Substitute.For<ILogger<TransientRetryBehaviour<TestCommand, TestResponse>>>(),
			options: options,
			transientFaultDetector: detector
		);
	}

	private static InvalidOperationException MakeTransient()
		=> new InvalidOperationException("connection reset");

	[Test]
	public async Task Handle_WhenNoException_ShouldCallNextOnce()
	{
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior();
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
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior();
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
	public async Task Handle_WhenTransientFaultThrown_ShouldRetryAndSucceed()
	{
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3);
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 1)
				throw MakeTransient();
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
	public async Task Handle_WhenAllRetriesFail_ShouldThrowOriginalException()
	{
		const int maxRetries = 2;
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeTransient());

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await behaviour.Handle(
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
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries);
		TestResponse expected = new TestResponse();
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();

		int callCount = 0;
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount <= maxRetries)
				throw MakeTransient();
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
	public async Task Handle_WhenTheDetectorRejectsTheFault_ShouldNotRetry()
	{
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3, transientFaults: false);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeTransient());

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenVersionConflictThrown_ShouldNotRetryHere()
	{
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 3, transientFaults: false);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => new ConcurrencyConflictException(message: "conflict", id: Guid.CreateVersion7()));

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenCancelledDuringDelay_ShouldThrowOperationCancelled()
	{
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 5, baseDelayMs: 5000, useJitter: false);

		using CancellationTokenSource cts = new CancellationTokenSource();

		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns<TestResponse>(returnThis: _ =>
		{
			cts.Cancel();
			throw MakeTransient();
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
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: maxRetries);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeTransient());

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: maxRetries + 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WithZeroMaxRetries_ShouldNotRetry()
	{
		TransientRetryBehaviour<TestCommand, TestResponse> behaviour = CreateBehavior(maxRetries: 0);
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Throws(createException: _ => MakeTransient());

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await behaviour.Handle(
			request: new TestCommand(),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}
}
