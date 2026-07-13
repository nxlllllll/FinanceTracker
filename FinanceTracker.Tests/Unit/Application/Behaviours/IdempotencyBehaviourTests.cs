using System.Text.Json;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class IdempotencyBehaviourTests
{
	public sealed record TestCommand(Guid IdempotencyKey) : IRequest<Result<Guid, DomainException>>, IIdempotentCommand;
	public sealed record NonIdempotentCommand : IRequest<Result<Guid, DomainException>>;
	public sealed record TestUserScopedCommand(Guid IdempotencyKey, Guid UserId) : IRequest<Result<Guid, DomainException>>, IIdempotentCommand, IUserScopedRequest;

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
	{
		Converters = { new ResultJsonConverterFactory() }
	};

	private IIdempotencyReadRepository _readRepository = null!;
	private IIdempotencyWriteRepository _writeRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IdempotencyBehaviour<TestCommand, Result<Guid, DomainException>> _behaviour = null!;

	private static readonly Guid ValidKey = Guid.CreateVersion7();
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IIdempotencyReadRepository>();
		_writeRepository = Substitute.For<IIdempotencyWriteRepository>();
		_writeRepository.TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<Guid, DomainException>>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task<Result<Guid, DomainException>>>>()?.Invoke());

		_behaviour = BuildBehavior<TestCommand>();
	}

	private IdempotencyBehaviour<TReq, Result<Guid, DomainException>> BuildBehavior<TReq>(
		int expiryHours = 24,
		int abandonedAfterSeconds = 30,
		IDateProvider? dateProvider = null)
		where TReq : notnull
	{
		return new IdempotencyBehaviour<TReq, Result<Guid, DomainException>>(
			idempotencyReadRepository: _readRepository,
			idempotencyWriteRepository: _writeRepository,
			unitOfWork: _unitOfWork,
			options: new FakeOptionsMonitor<IdempotencyOptions>(value: new IdempotencyOptions
			{
				ExpiryHours = expiryHours,
				InFlightInitialDelayMs = 0,
				InFlightMaxWaitMs = 100,
				InFlightMaxDelayMs = 50,
				AbandonedAfterSeconds = abandonedAfterSeconds
			}),
			dateProvider: dateProvider ?? FakeDateProvider.Default,
			logger: Substitute.For<ILogger<IdempotencyBehaviour<TReq, Result<Guid, DomainException>>>>()
		);
	}

	private static string Serialize(Result<Guid, DomainException> result)
		=> JsonSerializer.Serialize(value: result, options: JsonOpts);

	private static IdempotencyEntry CompletedEntry(Result<Guid, DomainException> result)
		=> new IdempotencyEntry(ResponseJson: Serialize(result: result), ReservedAt: Now);

	private static IdempotencyEntry InFlightEntry(DateTimeOffset? reservedAt = null)
		=> new IdempotencyEntry(ResponseJson: null, ReservedAt: reservedAt ?? Now);

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
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.DidNotReceive().TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenRequestIsNotIIdempotentCommand_ShouldNotOpenTransaction()
	{
		await BuildBehavior<NonIdempotentCommand>().Handle(
			request: new NonIdempotentCommand(),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<Guid, DomainException>>>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldReturnFailure()
	{
		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: Guid.Empty),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmptyIdempotencyKeyException>();
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldNotTouchRepositories()
	{
		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: Guid.Empty),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _readRepository.DidNotReceive().GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.DidNotReceive().TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenRequestIsUserScoped_ShouldPassUserIdToRepositories()
	{
		Guid userId = Guid.CreateVersion7();

		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		await BuildBehavior<TestUserScopedCommand>().Handle(
			request: new TestUserScopedCommand(IdempotencyKey: ValidKey, UserId: userId),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _readRepository.Received(requiredNumberOfCalls: 1).GetAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestUserScopedCommand),
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.Received(requiredNumberOfCalls: 1).TryReserveAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestUserScopedCommand),
			userId: userId,
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenRequestIsNotUserScoped_ShouldUseEmptyGuidAsUserId()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _readRepository.Received(requiredNumberOfCalls: 1).GetAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestCommand),
			userId: Guid.Empty,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquired_ShouldCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		bool nextCalled = false;

		await _behaviour.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
		{
			nextCalled = true;
			return Task.FromResult(result: Ok());
		}, cancellationToken: CancellationToken.None);

		await Assert.That(value: nextCalled).IsTrue();
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquired_ShouldRunNextInsideTheOuterTransaction()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _unitOfWork.Received(requiredNumberOfCalls: 1).ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<Guid, DomainException>>>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquiredAndSuccessResult_ShouldCompleteWithSerializedResponse()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		Guid expectedId = Guid.CreateVersion7();

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: OkWith(value: expectedId)),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).CompleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquiredAndFailureResult_ShouldDeleteKeyAndNotComplete()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Fail()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquiredAndNextThrows_ShouldDeleteKeyAndRethrow()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		InvalidOperationException thrown = new InvalidOperationException(message: "Simulated handler failure.");

		await Assert.That(async () => await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw thrown,
			cancellationToken: CancellationToken.None
		)).Throws<InvalidOperationException>();

		await _writeRepository.DidNotReceive().CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoCacheAndSlotAcquired_ShouldReserveWithCorrectExpiresAt()
	{
		const int expiryHours = 12;

		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		await BuildBehavior<TestCommand>(expiryHours: expiryHours).Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).TryReserveAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestCommand),
			userId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: expiryHours),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCompletedCacheFound_ShouldNotCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: CompletedEntry(result: Ok()));

		bool nextCalled = false;

		await _behaviour.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
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
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: CompletedEntry(result: OkWith(value: cachedId)));

		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: OkWith(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: cachedId);
	}

	[Test]
	public async Task Handle_WhenCompletedCacheFound_ShouldNotTouchWriteRepositoryOrTransaction()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: CompletedEntry(result: Ok()));

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.DidNotReceive().CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<Guid, DomainException>>>>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSlotAlreadyReserved_ShouldNotCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		_writeRepository.TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(
			returnThis: InFlightEntry(),
			returnThese: CompletedEntry(result: Ok())
		);

		bool nextCalled = false;

		await _behaviour.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
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

		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(
			returnThis: InFlightEntry(),
			returnThese: CompletedEntry(result: OkWith(value: completedId))
		);

		Result<Guid, DomainException> result = await _behaviour.Handle(
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
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: InFlightEntry(reservedAt: Now));

		Result<Guid, DomainException> result = await BuildBehavior<TestCommand>(abandonedAfterSeconds: 999).Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyTimeoutException>();
	}

	[Test]
	public async Task Handle_WhenEntryIsAbandoned_ShouldDeleteAndReturnFailure()
	{
		DateTimeOffset abandonedAt = Now.AddSeconds(seconds: -60);

		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: InFlightEntry(reservedAt: abandonedAt));

		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyAbandonedException>();
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenEntryBecomesAbandonedDuringPoll_ShouldDeleteAndReturnFailure()
	{
		DateTimeOffset abandonedAt = Now.AddSeconds(seconds: -60);

		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(
			returnThis: InFlightEntry(),
			returnThese: InFlightEntry(reservedAt: abandonedAt)
		);

		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyAbandonedException>();
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenEntryDisappearedDuringPoll_ShouldReturnFailure()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(
			returnThis: InFlightEntry(),
			returnThese: (IdempotencyEntry?)null
		);

		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyAbandonedException>();
	}
}
