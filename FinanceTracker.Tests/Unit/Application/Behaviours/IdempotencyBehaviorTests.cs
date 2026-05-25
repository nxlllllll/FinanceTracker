using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class IdempotencyBehaviorTests
{
	public sealed class JsonOptions
	{
		public System.Text.Json.JsonSerializerOptions Value { get; } = new System.Text.Json.JsonSerializerOptions
		{
			Converters = { new ResultJsonConverterFactory() }
		};
	}

	public  sealed record TestCommand(Guid IdempotencyKey) : IRequest<Result<Guid, DomainException>>, IIdempotentCommand;

	public  sealed record NonIdempotentCommand : IRequest<Result<Guid, DomainException>>;
	
	private IIdempotencyReadRepository _readRepository = null!;
	private IIdempotencyWriteRepository _writeRepository = null!;
	private IdempotencyBehavior<TestCommand, Result<Guid, DomainException>> _behavior = null!;

	private static readonly Guid ValidKey = Guid.CreateVersion7();
	private static readonly JsonOptions JsonOpts = new JsonOptions();

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IIdempotencyReadRepository>();
		_writeRepository = Substitute.For<IIdempotencyWriteRepository>();
		_behavior = BuildBehavior<TestCommand>();
	}

	private IdempotencyBehavior<TReq, Result<Guid, DomainException>> BuildBehavior<TReq>(int expiryHours = 24) where TReq : notnull
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
		=> System.Text.Json.JsonSerializer.Serialize(value: result, options: JsonOpts.Value);

	private static Result<Guid, DomainException> Ok()
		=> Result<Guid, DomainException>.Success(value: Guid.CreateVersion7());

	private static Result<Guid, DomainException> OkWith(Guid value)
		=> Result<Guid, DomainException>.Success(value: value);

	private static Result<Guid, DomainException> Fail()
		=> Result<Guid, DomainException>.Failure(error: new NotFoundException(message: "Not found.", id: Guid.Empty));
	
	[Test]
	public async Task Handle_WhenRequestIsNotIIdempotentCommand_ShouldNotCheckReadRepository()
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
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldNotCheckReadRepository()
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
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldNotCallWriteRepository()
	{
		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: Guid.Empty),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().StoreAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			responseJson: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCacheNotFound_ShouldCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		bool nextCalled = false;

		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => 
			{
				nextCalled = true; 
				return Task.FromResult(result: Ok()); 
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: nextCalled).IsTrue();
	}

	[Test]
	public async Task Handle_WhenCacheNotFoundAndSuccessResult_ShouldStoreViaWriteRepository()
	{
		_readRepository.GetAsync(
			idempotencyKey: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (string?)null);

		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).StoreAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestCommand),
			responseJson: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCacheNotFoundAndFailureResult_ShouldNotStoreViaWriteRepository()
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

		await _writeRepository.DidNotReceive().StoreAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			responseJson: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCacheFound_ShouldNotCallNext()
	{
		_readRepository.GetAsync(
			idempotencyKey: ValidKey,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Serialize(result: Ok()));
		bool nextCalled = false;

		await _behavior.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ =>
			{
				nextCalled = true;
				return Task.FromResult(result: Ok());
			},
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: nextCalled).IsFalse();
	}

	[Test]
	public async Task Handle_WhenCacheFound_ShouldReturnCachedResult()
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
	public async Task Handle_WhenCacheFound_ShouldNotCallWriteRepository()
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

		await _writeRepository.DidNotReceive().StoreAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			responseJson: Arg.Any<string>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenStoringResult_ShouldSetCorrectExpiresAt()
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

		await _writeRepository.Received(requiredNumberOfCalls: 1).StoreAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			responseJson: Arg.Any<string>(),
			expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: expiryHours),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
