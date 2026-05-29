using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class IdempotencyBehaviorTests
{
	public sealed record TestCommand(Guid IdempotencyKey) : IRequest<Result<Guid, DomainException>>, IIdempotentCommand;
	public sealed record NonIdempotentCommand : IRequest<Result<Guid, DomainException>>;

	private static readonly System.Text.Json.JsonSerializerOptions JsonOpts = new()
	{
		Converters = { new ResultJsonConverterFactory() }
	};

	private IIdempotencyReadRepository _readRepository = null!;
	private IIdempotencyWriteRepository _writeRepository = null!;
	private IdempotencyBehavior<TestCommand, Result<Guid, DomainException>> _behavior = null!;

	private static readonly Guid ValidKey = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IIdempotencyReadRepository>();
		_writeRepository = Substitute.For<IIdempotencyWriteRepository>();
		_writeRepository.TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		_behavior = BuildBehavior<TestCommand>();
	}

	private IdempotencyBehavior<TReq, Result<Guid, DomainException>> BuildBehavior<TReq>(int expiryHours = 24)
		where TReq : notnull
	{
		return new IdempotencyBehavior<TReq, Result<Guid, DomainException>>(
			idempotencyReadRepository: _readRepository,
			idempotencyWriteRepository: _writeRepository,
			options: new FakeOptionsMonitor<IdempotencyOptions>(value: new IdempotencyOptions { ExpiryHours = expiryHours }),
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<IdempotencyBehavior<TReq, Result<Guid, DomainException>>>>()
		);
	}

	private static string Serialize(Result<Guid, DomainException> result)
		=> System.Text.Json.JsonSerializer.Serialize(value: result, options: JsonOpts);

	private static Result<Guid, DomainException> Ok()
		=> Result<Guid, DomainException>.Success(value: Guid.CreateVersion7());

	private static Result<Guid, DomainException> OkWith(Guid value)
		=> Result<Guid, DomainException>.Success(value: value);

	private static Result<Guid, DomainException> Fail()
		=> Result<Guid, DomainException>.Failure(error: new NotFoundException(message: "Not found.", id: Guid.Empty));

	[Test]
	public async Task Handle_WhenRequestIsNotIIdempotentCommand_ShouldSkipAllIdempotencyLogic()
	{
		await BuildBehavior<NonIdempotentCommand>().Handle(
			request: new NonIdempotentCommand(),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _readRepository.DidNotReceive().GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.DidNotReceive().TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldReturnFailure()
	{
		Result<Guid, DomainException> result = await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: Guid.Empty),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmptyIdempotentException>();
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldNotTouchRepositories()
	{
		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: Guid.Empty),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _readRepository.DidNotReceive().GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.DidNotReceive().TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquired_ShouldCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		bool nextCalled = false;

		await _behavior.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
		{
			nextCalled = true;
			return Task.FromResult(result: Ok());
		}, cancellationToken: CancellationToken.None);

		await Assert.That(value: nextCalled).IsTrue();
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquiredAndSuccessResult_ShouldCompleteWithSerializedResponse()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		Guid expectedId = Guid.CreateVersion7();

		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: OkWith(value: expectedId)),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).CompleteAsync(
			idempotencyKey: ValidKey,
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquiredAndFailureResult_ShouldNotComplete()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Fail()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquired_ShouldReserveWithCorrectExpiresAt()
	{
		const int expiryHours = 12;

		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		await BuildBehavior<TestCommand>(expiryHours: expiryHours).Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).TryReserveAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestCommand),
			expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: expiryHours),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCompletedCacheFound_ShouldNotCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Serialize(result: Ok()));

		bool nextCalled = false;

		await _behavior.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
		{
			nextCalled = true;
			return Task.FromResult(result: Ok());
		}, cancellationToken: CancellationToken.None);

		await Assert.That(value: nextCalled).IsFalse();
	}

	[Test]
	public async Task Handle_WhenCompletedCacheFound_ShouldReturnCachedResult()
	{
		Guid cachedId = Guid.CreateVersion7();

		_readRepository.GetAsync(
			idempotencyKey: ValidKey, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Serialize(result: OkWith(value: cachedId)));

		Result<Guid, DomainException> result = await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: OkWith(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: cachedId);
	}

	[Test]
	public async Task Handle_WhenCompletedCacheFound_ShouldNotTouchWriteRepository()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Serialize(result: Ok()));

		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.DidNotReceive().CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSlotAlreadyReserved_ShouldNotCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		_writeRepository.TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		string completedJson = Serialize(result: Ok());
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: null, String.Empty, completedJson);

		bool nextCalled = false;

		await _behavior.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
		{
			nextCalled = true;
			return Task.FromResult(result: Ok());
		}, cancellationToken: CancellationToken.None);

		await Assert.That(value: nextCalled).IsFalse();
	}

	[Test]
	public async Task Handle_WhenInFlightEntryFound_ShouldPollUntilCompleted()
	{
		Guid completedId = Guid.CreateVersion7();
		string completedJson = Serialize(result: OkWith(value: completedId));

		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: String.Empty, returnThese: completedJson);

		Result<Guid, DomainException> result = await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: completedId);
	}

	[Test]
	public async Task Handle_WhenInFlightTimesOut_ShouldReturnFailure()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: String.Empty);

		using CancellationTokenSource cts = new CancellationTokenSource(delay: TimeSpan.FromSeconds(value: 10));

		Result<Guid, DomainException> result = await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: cts.Token
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmptyIdempotentException>();
	}
}