using System.Net;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.RateLimit;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.RateLimit;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class AuthRateLimitingBehaviourTests
{
	public sealed record TestCommandNeitherScope : IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;

	public sealed record TestCommandIpOnly(IPAddress IpAddress)
		: IIpScopedRequest, IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;

	public sealed record TestCommandEmailOnly(Email Email)
		: IEmailScopedRequest, IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;

	public sealed record TestCommandBoth(IPAddress IpAddress, Email Email)
		: IIpScopedRequest, IEmailScopedRequest, IRequest<Result<FinanceTracker.Core.Results.Unit, DomainException>>;

	private static readonly AnonymousRateLimitOptions DefaultOptions = new AnonymousRateLimitOptions
	{
		IpRequestsPerWindow = 20,
		IpWindowSeconds = 300,
		EmailRequestsPerWindow = 5,
		EmailWindowSeconds = 300
	};

	private static readonly Email TestEmail = Email.Create(value: "test@test.com").Value!;
	private readonly IPAddress _testIp = IPAddress.Parse(ipString: "203.0.113.10");

	private IRateLimiter _rateLimiter = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_rateLimiter = Substitute.For<IRateLimiter>();
	}

	private AuthRateLimitingBehaviour<TRequest, Result<FinanceTracker.Core.Results.Unit, DomainException>> CreateBehaviour<TRequest>(
		AnonymousRateLimitOptions? options = null)
		where TRequest : notnull
	{
		return new AuthRateLimitingBehaviour<TRequest, Result<FinanceTracker.Core.Results.Unit, DomainException>>(
			rateLimiter: _rateLimiter,
			options: new FakeOptionsMonitor<AnonymousRateLimitOptions>(options ?? DefaultOptions)
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

	private void AllowAllRateLimiterCalls()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Allowed());
	}

	[Test]
	public async Task Handle_WhenRequestImplementsNeitherScope_ShouldNotCallRateLimiter()
	{
		AuthRateLimitingBehaviour<TestCommandNeitherScope, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandNeitherScope>();

		await behaviour.Handle(
			request: new TestCommandNeitherScope(),
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
	public async Task Handle_WhenRequestImplementsNeitherScope_ShouldCallNext()
	{
		AuthRateLimitingBehaviour<TestCommandNeitherScope, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandNeitherScope>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		await behaviour.Handle(
			request: new TestCommandNeitherScope(),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenIpScopedAndAllowed_ShouldCallNext()
	{
		AllowAllRateLimiterCalls();
		AuthRateLimitingBehaviour<TestCommandIpOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandIpOnly>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		await behaviour.Handle(
			request: new TestCommandIpOnly(IpAddress: _testIp),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenIpScopedAndDenied_ShouldReturnFailureAndNotCallNext()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Denied(retryAfterSeconds: 60));

		AuthRateLimitingBehaviour<TestCommandIpOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandIpOnly>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await behaviour.Handle(
			request: new TestCommandIpOnly(IpAddress: _testIp),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<RateLimitExceededException>();
		await next.DidNotReceive().Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenIpScoped_ShouldUseIpKeyAndIpOptions()
	{
		AnonymousRateLimitOptions customOptions = new AnonymousRateLimitOptions
		{
			IpRequestsPerWindow = 7,
			IpWindowSeconds = 111,
			EmailRequestsPerWindow = 99,
			EmailWindowSeconds = 999
		};
		AllowAllRateLimiterCalls();
		AuthRateLimitingBehaviour<TestCommandIpOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandIpOnly>(options: customOptions);

		await behaviour.Handle(
			request: new TestCommandIpOnly(IpAddress: _testIp),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 1).IsAllowedAsync(
			key: $"ratelimit:ip:{_testIp}",
			requestsPerWindow: customOptions.IpRequestsPerWindow,
			windowSeconds: customOptions.IpWindowSeconds,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenEmailScopedAndAllowed_ShouldCallNext()
	{
		AllowAllRateLimiterCalls();
		AuthRateLimitingBehaviour<TestCommandEmailOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandEmailOnly>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		await behaviour.Handle(
			request: new TestCommandEmailOnly(Email: TestEmail),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenEmailScopedAndDenied_ShouldReturnFailureAndNotCallNext()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Any<string>(),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Denied(retryAfterSeconds: 60));

		AuthRateLimitingBehaviour<TestCommandEmailOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandEmailOnly>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await behaviour.Handle(
			request: new TestCommandEmailOnly(Email: TestEmail),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<RateLimitExceededException>();
		await next.DidNotReceive().Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenEmailScoped_ShouldUseEmailKeyAndEmailOptions()
	{
		AnonymousRateLimitOptions customOptions = new AnonymousRateLimitOptions
		{
			IpRequestsPerWindow = 99,
			IpWindowSeconds = 999,
			EmailRequestsPerWindow = 3,
			EmailWindowSeconds = 77
		};
		AllowAllRateLimiterCalls();
		AuthRateLimitingBehaviour<TestCommandEmailOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandEmailOnly>(options: customOptions);

		await behaviour.Handle(
			request: new TestCommandEmailOnly(Email: TestEmail),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 1).IsAllowedAsync(
			key: $"ratelimit:email:{TestEmail.Value}",
			requestsPerWindow: customOptions.EmailRequestsPerWindow,
			windowSeconds: customOptions.EmailWindowSeconds,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenBothScopedAndBothAllow_ShouldCheckBothLimitsAndCallNext()
	{
		AllowAllRateLimiterCalls();
		AuthRateLimitingBehaviour<TestCommandBoth, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandBoth>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		await behaviour.Handle(
			request: new TestCommandBoth(IpAddress: _testIp, Email: TestEmail),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 1).IsAllowedAsync(
			key: $"ratelimit:ip:{_testIp}",
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _rateLimiter.Received(requiredNumberOfCalls: 1).IsAllowedAsync(
			key: $"ratelimit:email:{TestEmail.Value}",
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
		await next.Received(requiredNumberOfCalls: 1).Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenBothScopedAndOnlyIpDenies_ShouldReturnFailureAndNotCheckEmail()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Is<string>(predicate: k => k!.StartsWith(value: "ratelimit:ip:")),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Denied(retryAfterSeconds: 60));
		_rateLimiter.IsAllowedAsync(
			key: Arg.Is<string>(predicate: k => k!.StartsWith(value: "ratelimit:email:")),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Allowed());

		AuthRateLimitingBehaviour<TestCommandBoth, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandBoth>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await behaviour.Handle(
			request: new TestCommandBoth(IpAddress: _testIp, Email: TestEmail),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<RateLimitExceededException>();
		await next.DidNotReceive().Invoke(t: Arg.Any<CancellationToken>());
		await _rateLimiter.DidNotReceive().IsAllowedAsync(
			key: Arg.Is<string>(predicate: k => k!.StartsWith(value: "ratelimit:email:")),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenBothScopedAndOnlyEmailDenies_ShouldReturnFailure()
	{
		_rateLimiter.IsAllowedAsync(
			key: Arg.Is<string>(predicate: k => k!.StartsWith(value: "ratelimit:ip:")),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Allowed());
		_rateLimiter.IsAllowedAsync(
			key: Arg.Is<string>(predicate: k => k!.StartsWith(value: "ratelimit:email:")),
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RateLimitResult.Denied(retryAfterSeconds: 60));

		AuthRateLimitingBehaviour<TestCommandBoth, Result<FinanceTracker.Core.Results.Unit, DomainException>> behaviour = CreateBehaviour<TestCommandBoth>();
		RequestHandlerDelegate<Result<FinanceTracker.Core.Results.Unit, DomainException>> next = AllowedNext();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await behaviour.Handle(
			request: new TestCommandBoth(IpAddress: _testIp, Email: TestEmail),
			next: next,
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<RateLimitExceededException>();
		await next.DidNotReceive().Invoke(t: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WhenEmailScoped_ForDifferentCommandTypes_ShouldShareTheSameBucket()
	{
		AllowAllRateLimiterCalls();

		AuthRateLimitingBehaviour<TestCommandEmailOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> emailOnlyBehaviour = CreateBehaviour<TestCommandEmailOnly>();
		await emailOnlyBehaviour.Handle(
			request: new TestCommandEmailOnly(Email: TestEmail),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		AuthRateLimitingBehaviour<TestCommandBoth, Result<FinanceTracker.Core.Results.Unit, DomainException>> bothBehaviour = CreateBehaviour<TestCommandBoth>();
		await bothBehaviour.Handle(
			request: new TestCommandBoth(IpAddress: _testIp, Email: TestEmail),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 2).IsAllowedAsync(
			key: $"ratelimit:email:{TestEmail.Value}",
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenIpScoped_ForDifferentCommandTypes_ShouldShareTheSameBucket()
	{
		AllowAllRateLimiterCalls();

		AuthRateLimitingBehaviour<TestCommandIpOnly, Result<FinanceTracker.Core.Results.Unit, DomainException>> ipOnlyBehaviour = CreateBehaviour<TestCommandIpOnly>();
		await ipOnlyBehaviour.Handle(
			request: new TestCommandIpOnly(IpAddress: _testIp),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		AuthRateLimitingBehaviour<TestCommandBoth, Result<FinanceTracker.Core.Results.Unit, DomainException>> bothBehaviour = CreateBehaviour<TestCommandBoth>();
		await bothBehaviour.Handle(
			request: new TestCommandBoth(IpAddress: _testIp, Email: TestEmail),
			next: AllowedNext(),
			cancellationToken: CancellationToken.None
		);

		await _rateLimiter.Received(requiredNumberOfCalls: 2).IsAllowedAsync(
			key: $"ratelimit:ip:{_testIp}",
			requestsPerWindow: Arg.Any<int>(),
			windowSeconds: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
