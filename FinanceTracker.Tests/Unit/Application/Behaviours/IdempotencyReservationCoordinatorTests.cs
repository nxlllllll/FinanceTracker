using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Behaviours;

public sealed class IdempotencyReservationCoordinatorTests
{
	private IIdempotencyReadRepository _readRepository = null!;
	private IIdempotencyWriteRepository _writeRepository = null!;
	private IdempotencyReservationCoordinator _coordinator = null!;

	private static readonly Guid Key = Guid.CreateVersion7();
	private const string CommandType = "TestCommand";
	private static readonly Guid UserId = Guid.CreateVersion7();
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
			reservationId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		_coordinator = BuildCoordinator();
	}

	private IdempotencyReservationCoordinator BuildCoordinator(int abandonedAfterSeconds = 30)
	{
		return new IdempotencyReservationCoordinator(
			readRepository: _readRepository,
			writeRepository: _writeRepository,
			options: new FakeOptionsMonitor<IdempotencyOptions>(value: new IdempotencyOptions
			{
				ExpiryHours = 24,
				InFlightInitialDelayMs = 0,
				InFlightMaxWaitMs = 100,
				InFlightMaxDelayMs = 50,
				AbandonedAfterSeconds = abandonedAfterSeconds
			}),
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<IdempotencyReservationCoordinator>>()
		);
	}

	private static IdempotencyEntry CompletedEntry(string responseJson) => new IdempotencyEntry(
		ReservationId: Guid.CreateVersion7(),
		ResponseJson: responseJson,
		ReservedAt: Now
	);

	private static IdempotencyEntry InFlightEntry(Guid? reservationId = null, DateTimeOffset? reservedAt = null) => new IdempotencyEntry(
		ReservationId: reservationId ?? Guid.CreateVersion7(),
		ResponseJson: null,
		ReservedAt: reservedAt ?? Now
	);

	[Test]
	public async Task AcquireAsync_WhenNoEntryExists_ShouldReturnReserved()
	{
		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.Reserved);
		await Assert.That(value: acquisition.ReservationId).IsNotEqualTo(notExpected: Guid.Empty);
	}

	[Test]
	public async Task AcquireAsync_WhenNoEntryExists_ShouldReserveWithExpiryFromOptions()
	{
		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (IdempotencyEntry?)null);

		await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).TryReserveAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			reservationId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Now.AddHours(hours: 24),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task AcquireAsync_WhenCompletedEntryExists_ShouldReturnCachedResponse()
	{
		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: CompletedEntry(responseJson: """{"cached":true}"""));

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.CachedResponse);
		await Assert.That(value: acquisition.CachedResponseJson).IsEqualTo(expected: """{"cached":true}""");
	}

	[Test]
	public async Task AcquireAsync_WhenReservationAlreadyTaken_ShouldPollUntilCompleted()
	{
		_writeRepository.TryReserveAsync(
			idempotencyKey: Arg.Any<Guid>(),
			commandType: Arg.Any<string>(),
			userId: Arg.Any<Guid>(),
			reservationId: Arg.Any<Guid>(),
			reservedAt: Arg.Any<DateTimeOffset>(),
			expiresAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: null, returnThese: CompletedEntry(responseJson: """{"cached":true}"""));

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.CachedResponse);
	}

	[Test]
	public async Task AcquireAsync_WhenInFlightAndFresh_ShouldPollUntilCompleted()
	{
		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: InFlightEntry(), returnThese: CompletedEntry(responseJson: """{"cached":true}"""));

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.CachedResponse);
	}

	[Test]
	public async Task AcquireAsync_WhenInFlightNeverCompletes_ShouldTimeOut()
	{
		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: InFlightEntry());

		IdempotencyAcquisition acquisition = await BuildCoordinator(abandonedAfterSeconds: 999).AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.Failed);
		await Assert.That(value: acquisition.Error).IsTypeOf<IdempotencyTimeoutException>();
	}

	[Test]
	public async Task AcquireAsync_WhenEntryIsAbandoned_ShouldReclaimAndReturnFailure()
	{
		DateTimeOffset abandonedAt = Now.AddSeconds(seconds: -60);
		Guid staleReservationId = Guid.CreateVersion7();

		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: InFlightEntry(reservationId: staleReservationId, reservedAt: abandonedAt));

		_writeRepository.DeleteAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			reservationId: staleReservationId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.Failed);
		await Assert.That(value: acquisition.Error).IsTypeOf<IdempotencyAbandonedException>();
		await _writeRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			reservationId: staleReservationId,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task AcquireAsync_WhenReclaimRacesWithAnotherRequest_ShouldReCheckInsteadOfDeclaringAbandoned()
	{
		DateTimeOffset abandonedAt = Now.AddSeconds(seconds: -60);
		Guid staleReservationId = Guid.CreateVersion7();

		_writeRepository.DeleteAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			reservationId: staleReservationId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(
			returnThis: InFlightEntry(reservationId: staleReservationId, reservedAt: abandonedAt),
			returnThese: CompletedEntry(responseJson: """{"cached":true}""")
		);

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.CachedResponse);
	}

	[Test]
	public async Task AcquireAsync_WhenEntryDisappearsDuringPoll_ShouldReturnFailure()
	{
		_readRepository.GetAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: InFlightEntry(), returnThese: (IdempotencyEntry?)null);

		IdempotencyAcquisition acquisition = await _coordinator.AcquireAsync(
			idempotencyKey: Key,
			commandType: CommandType,
			userId: UserId,
			ct: CancellationToken.None
		);

		await Assert.That(value: acquisition.Kind).IsEqualTo(expected: IdempotencyAcquisitionKind.Failed);
		await Assert.That(value: acquisition.Error).IsTypeOf<IdempotencyAbandonedException>();
	}
}
