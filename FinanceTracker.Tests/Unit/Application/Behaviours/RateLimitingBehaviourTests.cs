using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.RateLimit;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class RateLimitingBehaviourTests
{
	public sealed record TestCommand(Guid UserId) : IUserScopedRequest, IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;
	public sealed record TestQuery(Guid UserId) : IUserScopedRequest, IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;
	public sealed record TestCommandWithoutScope : IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;

	private static readonly RateLimitOptions DefaultOptions = new RateLimitOptions
	{
		RequestsPerWindow = 60,
		WindowSeconds = 60
	};

	private IRateLimiter _rateLimiter = null!;
	private RateLimitingBehaviour<TestCommand, Result<FinanceTracker.Core.Results.Unit, DomainException>> _behaviour = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_rateLimiter = Substitute.For<IRateLimiter>();
		_behaviour = CreateCommandBehavior();
	}

	private RateLimitingBehaviour<TestCommand, Result<FinanceTracker.Core.Results.Unit, DomainException>> CreateCommandBehavior(RateLimitOptions? options = null)
	{
		return new RateLimitingBehaviour<TestCommand, Result<FinanceTracker.Core.Results.Unit, DomainException>>(
			rateLimiter: _rateLimiter,
			options: new FakeOptionsMonitor<RateLimitOptions>(options ?? DefaultOptions)
		);
	}

	private static RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> AllowedNext()
	{
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next =
			Substitute.For<RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>>>();

		next(
			t: Arg.Any<CancellationToken>()
		).Returns(returnThis: Result<FinanceTracker.Core.Results.Unit, DomainException>.Success(value: FinanceTracker.Core.Results.Unit.Default));
		return next;
	}

	[Test]
	public async Task Handle_WhenRequestIsNotUserScoped_ShouldNotCallRateLimiter()
	{
		RateLimitingBehaviour<TestCommandWithoutScope, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour =
			new RateLimitingBehaviour<TestCommandWithoutScope, Result<FinanceTracker.Core.Results.Unit, DomainException>>(
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
		RateLimitingBehaviour<TestCommandWithoutScope, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour =
			new RateLimitingBehaviour<TestCommandWithoutScope, Result<FinanceTracker.Core.Results.Unit, DomainException>>(
				rateLimiter: _rateLimiter,
				options: new FakeOptionsMonitor<RateLimitOptions>(value: DefaultOptions)
			);

		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

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

		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenRateLimiterAllows_ShouldReturnSuccess()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task Handle_WhenRateLimiterDenies_ShouldReturnFailure()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<RateLimitExceededException>();
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

		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		await _behaviour.Handle(
			request: new TestCommand(UserId: Guid.CreateVersion7()),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.DidNotReceive().Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCallRateLimiterWithCorrectKey()
	{
		Guid userId = Guid.CreateVersion7();
		string expectedKey = $"ratelimit:user:{userId}";
		string? capturedKey = null;

		_rateLimiter.IsAllowedAsync(
			key: Arg.Do<string>(useArgument: x => capturedKey = x),
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
	public async Task Handle_ForDifferentCommandTypes_SameUser_ShouldShareTheSameBucket()
	{
		Guid userId = Guid.CreateVersion7();

		string? keyFromCommand = null;
		string? keyFromQuery = null;

		_rateLimiter.IsAllowedAsync(
			key: Arg.Do<string>(useArgument: k => keyFromCommand = k),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		await _behaviour.Handle(
			request: new TestCommand(UserId: userId),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		RateLimitingBehaviour<TestQuery, Result<FinanceTracker.Core.Results.Unit, DomainException>> queryBehaviour =
			new RateLimitingBehaviour<TestQuery, Result<FinanceTracker.Core.Results.Unit, DomainException>>(
				rateLimiter: _rateLimiter,
				options: new FakeOptionsMonitor<RateLimitOptions>(value: DefaultOptions)
			);

		_rateLimiter.IsAllowedAsync(
			key: Arg.Do<string>(useArgument: k => keyFromQuery = k),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		await queryBehaviour.Handle(
			request: new TestQuery(UserId: userId),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: keyFromCommand).IsEqualTo(expected: keyFromQuery)
			.Because(message: "TestCommand and TestQuery are different request types for the same user — they must consume the same budget, not two separate 60/window allowances.");
	}

	[Test]
	public async Task Handle_ShouldCallRateLimiterWithCorrectOptions()
	{
		RateLimitOptions customOptions = new RateLimitOptions { RequestsPerWindow = 10, WindowSeconds = 30 };
		RateLimitingBehaviour<TestCommand, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateCommandBehavior(options: customOptions);

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

		RateLimitingBehaviour<TestQuery, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour =
			new RateLimitingBehaviour<TestQuery, Result<FinanceTracker.Core.Results.Unit, DomainException>>(
				rateLimiter: _rateLimiter,
				options: new FakeOptionsMonitor<RateLimitOptions>(DefaultOptions)
			);

		await behaviour.Handle(
			request: new TestQuery(UserId: Guid.CreateVersion7()),
			next: Substitute.For<RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>>>(),
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
