using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Services.RateLimit;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class RateLimitingBehaviourTests
{
	public sealed record TestCommand(Guid UserId) : IUserScopedRequest, IRequest<TestResponse>;
	public sealed record TestQuery(Guid UserId) : IUserScopedRequest, IRequest<TestResponse>;
	public sealed record TestCommandWithoutScope : IRequest<TestResponse>;
	public sealed class TestResponse;

	private static readonly RateLimitOptions DefaultOptions = new RateLimitOptions
	{
		RequestsPerWindow = 60,
		WindowSeconds = 60
	};

	private IRateLimiter _rateLimiter = null!;
	private RateLimitingBehaviour<TestCommand, TestResponse> _behaviour = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_rateLimiter = Substitute.For<IRateLimiter>();
		_behaviour = CreateCommandBehavior();
	}

	private RateLimitingBehaviour<TestCommand, TestResponse> CreateCommandBehavior(
		RateLimitOptions? options = null)
	{
		return new RateLimitingBehaviour<TestCommand, TestResponse>(
			rateLimiter: _rateLimiter,
			options: new FakeOptionsMonitor<RateLimitOptions>(options ?? DefaultOptions)
		);
	}

	private static RequestHandlerDelegate<TestResponse> AllowedNext()
	{
		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: new TestResponse());
		return next;
	}

	[Test]
	public async Task Handle_WhenRequestIsNotUserScoped_ShouldNotCallRateLimiter()
	{
		RateLimitingBehaviour<TestCommandWithoutScope, TestResponse> behaviour = new RateLimitingBehaviour<TestCommandWithoutScope, TestResponse>(
			rateLimiter: _rateLimiter,
			options: new FakeOptionsMonitor<RateLimitOptions>(value: DefaultOptions)
		);

		await behaviour.Handle(
			request: new TestCommandWithoutScope(),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.DidNotReceive().IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenRequestIsNotUserScoped_ShouldCallNext()
	{
		RateLimitingBehaviour<TestCommandWithoutScope, TestResponse> behaviour = new RateLimitingBehaviour<TestCommandWithoutScope, TestResponse>(
			rateLimiter: _rateLimiter,
			options: new FakeOptionsMonitor<RateLimitOptions>(value: DefaultOptions)
		);

		RequestHandlerDelegate<TestResponse> next = AllowedNext();

		await behaviour.Handle(
			request: new TestCommandWithoutScope(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenRateLimiterAllows_ShouldCallNext()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		RequestHandlerDelegate<TestResponse> next = AllowedNext();

		await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenRateLimiterAllows_ShouldReturnResponse()
	{
		TestResponse expected = new TestResponse();

		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		RequestHandlerDelegate<TestResponse> next = Substitute.For<RequestHandlerDelegate<TestResponse>>();
		next(t: Arg.Any<CancellationToken>()).Returns(returnThis: expected);

		TestResponse result = await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task Handle_WhenRateLimiterDenies_ShouldThrowRateLimitExceededException()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		await Assert.ThrowsAsync<RateLimitExceededException>(action: async () => await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		));
	}

	[Test]
	public async Task Handle_WhenRateLimiterDenies_ShouldNotCallNext()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		RequestHandlerDelegate<TestResponse> next = AllowedNext();

		await Assert.ThrowsAsync<RateLimitExceededException>(action: async () => await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: next,
			cancellationToken: CancellationToken.None
		));

		await next.DidNotReceive().Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCallRateLimiterWithCorrectKey()
	{
		Guid userId = Guid.CreateVersion7();
		string expectedKey = $"ratelimit:{nameof(TestCommand)}:{userId}";
		string? capturedKey = null;

		_rateLimiter.IsAllowedAsync(
			key: Arg.Do<string>(x => capturedKey = x),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		await _behaviour.Handle(
			request: new TestCommand(UserId: userId),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: capturedKey).IsEqualTo(expected: expectedKey);
	}

	[Test]
	public async Task Handle_ShouldCallRateLimiterWithCorrectOptions()
	{
		RateLimitOptions customOptions = new RateLimitOptions { RequestsPerWindow = 10, WindowSeconds = 30 };
		RateLimitingBehaviour<TestCommand, TestResponse> behaviour = CreateCommandBehavior(options: customOptions);

		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		await behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 1).IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: customOptions.RequestsPerWindow,
			windowSeconds: customOptions.WindowSeconds,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenUserScopedQuery_ShouldCallRateLimiter()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		RateLimitingBehaviour<TestQuery, TestResponse> behaviour = new RateLimitingBehaviour<TestQuery, TestResponse>(
			rateLimiter: _rateLimiter,
			options: new FakeOptionsMonitor<RateLimitOptions>(DefaultOptions)
		);

		RequestHandlerDelegate<TestResponse> next = AllowedNext();

		await behaviour.Handle(
			request: new TestQuery(UserId: Guid.CreateVersion7()),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 1).IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
