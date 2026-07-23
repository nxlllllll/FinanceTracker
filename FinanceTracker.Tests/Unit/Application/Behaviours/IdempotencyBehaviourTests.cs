using System.Text.Json;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Results;
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

	private IIdempotencyReservationCoordinator _coordinator = null!;
	private IIdempotencyWriteRepository _writeRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IdempotencyBehaviour<TestCommand, Result<Guid, DomainException>> _behaviour = null!;

	private static readonly Guid ValidKey = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_coordinator = Substitute.For<IIdempotencyReservationCoordinator>();
		_writeRepository = Substitute.For<IIdempotencyWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Result<Guid, DomainException>>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task<Result<Guid, DomainException>>>>()?.Invoke());

		_behaviour = BuildBehavior<TestCommand>();
	}

	private IdempotencyBehaviour<TReq, Result<Guid, DomainException>> BuildBehavior<TReq>() where TReq : notnull
	{
		return new IdempotencyBehaviour<TReq, Result<Guid, DomainException>>(
			coordinator: _coordinator,
			idempotencyWriteRepository: _writeRepository,
			unitOfWork: _unitOfWork,
			logger: Substitute.For<ILogger<IdempotencyBehaviour<TReq, Result<Guid, DomainException>>>>()
		);
	}

	private static string Serialize(Result<Guid, DomainException> result)
		=> JsonSerializer.Serialize(value: result, options: JsonOpts);

	private static Result<Guid, DomainException> Ok()
		=> Result<Guid, DomainException>.Success(value: Guid.CreateVersion7());

	private static Result<Guid, DomainException> OkWith(Guid value)
		=> Result<Guid, DomainException>.Success(value: value);

	private static Result<Guid, DomainException> Fail()
		=> Result<Guid, DomainException>.Failure(error: new NotFoundException(message: "Not found.", id: Guid.Empty));

	private void GivenReserved(Guid reservationId)
	{
		_coordinator.AcquireAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: IdempotencyAcquisition.Reserved(reservationId: reservationId));
	}

	private void GivenCached(Result<Guid, DomainException> result)
	{
		_coordinator.AcquireAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: IdempotencyAcquisition.CachedResponse(json: Serialize(result: result)));
	}

	private void GivenFailed(DomainException error)
	{
		_coordinator.AcquireAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: IdempotencyAcquisition.Failed(error: error));
	}

	[Test]
	public async Task Handle_WhenRequestIsNotIIdempotentCommand_ShouldSkipCoordinator()
	{
		await BuildBehavior<NonIdempotentCommand>().Handle(
			request: new NonIdempotentCommand(),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _coordinator.DidNotReceive().AcquireAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenIdempotencyKeyIsEmpty_ShouldReturnFailureWithoutCallingCoordinator()
	{
		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: Guid.Empty),
			next: _ => throw new InvalidOperationException(message: "next should not be called"),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmptyIdempotencyKeyException>();
		await _coordinator.DidNotReceive().AcquireAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenRequestIsUserScoped_ShouldPassUserIdToCoordinator()
	{
		Guid userId = Guid.CreateVersion7();
		GivenReserved(reservationId: Guid.CreateVersion7());

		await BuildBehavior<TestUserScopedCommand>().Handle(
			request: new TestUserScopedCommand(IdempotencyKey: ValidKey, UserId: userId),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _coordinator.Received(requiredNumberOfCalls: 1).AcquireAsync(
			idempotencyKey: ValidKey,
			commandType: nameof(TestUserScopedCommand),
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCoordinatorReturnsCachedResponse_ShouldNotCallNext()
	{
		GivenCached(result: OkWith(value: Guid.CreateVersion7()));

		bool nextCalled = false;

		await _behaviour.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
		{
			nextCalled = true;
			return Task.FromResult(result: Ok());
		}, cancellationToken: CancellationToken.None);

		await Assert.That(value: nextCalled).IsFalse();
	}

	[Test]
	public async Task Handle_WhenCoordinatorReturnsCachedResponse_ShouldReturnDeserializedResult()
	{
		Guid cachedId = Guid.CreateVersion7();
		GivenCached(result: OkWith(value: cachedId));

		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: OkWith(value: Guid.NewGuid())),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: cachedId);
	}

	[Test]
	public async Task Handle_WhenCoordinatorReturnsFailed_ShouldReturnFailureWithoutCallingNext()
	{
		GivenFailed(error: new IdempotencyTimeoutException(message: "Timed out."));

		bool nextCalled = false;

		Result<Guid, DomainException> result = await _behaviour.Handle(request: new TestCommand(IdempotencyKey: ValidKey), next: _ =>
		{
			nextCalled = true;
			return Task.FromResult(result: Ok());
		}, cancellationToken: CancellationToken.None);

		await Assert.That(value: nextCalled).IsFalse();
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyTimeoutException>();
	}

	[Test]
	public async Task Handle_WhenReserved_ShouldCallNextInsideTheTransaction()
	{
		GivenReserved(reservationId: Guid.CreateVersion7());

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
	public async Task Handle_WhenReservedAndNextSucceeds_ShouldCompleteWithThatReservationId()
	{
		Guid reservationId = Guid.CreateVersion7();
		GivenReserved(reservationId: reservationId);

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).CompleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: reservationId,
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenReservedAndNextFails_ShouldReleaseAndNotComplete()
	{
		Guid reservationId = Guid.CreateVersion7();
		GivenReserved(reservationId: reservationId);

		await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Fail()),
			cancellationToken: CancellationToken.None
		);

		await _writeRepository.DidNotReceive().CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: reservationId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenReservedAndNextThrows_ShouldReleaseAndRethrow()
	{
		Guid reservationId = Guid.CreateVersion7();
		GivenReserved(reservationId: reservationId);

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
			reservationId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: reservationId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCompleteAsyncReportsReservationLost_ShouldReleaseAndReturnFailureWithoutThrowing()
	{
		Guid reservationId = Guid.CreateVersion7();
		GivenReserved(reservationId: reservationId);

		_writeRepository.CompleteAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: Arg.Any<Guid>(),
			responseJson: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		Result<Guid, DomainException> result = await _behaviour.Handle(
			request: new TestCommand(IdempotencyKey: ValidKey),
			next: _ => Task.FromResult(result: Ok()),
			cancellationToken: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IdempotencyReservationLostException>();
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: ValidKey,
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: reservationId,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
