using System.Text.Json;
using FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Infrastructure.Database.Repositories.Idempotency;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Idempotency;

public sealed class IdempotencyRepositoryTests : DatabaseFixture
{
	private IdempotencyReadRepository _readRepository = null!;
	private IdempotencyWriteRepository _writeRepository = null!;

	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new IdempotencyReadRepository(context: Context);
		_writeRepository = new IdempotencyWriteRepository(context: Context);
	}

	[Test]
	public async Task TryReserveAsync_WithSameKeyDifferentUsers_ShouldReserveBothIndependently()
	{
		Guid key = Guid.CreateVersion7();
		Guid userA = Guid.CreateVersion7();
		Guid userB = Guid.CreateVersion7();

		bool reservedA = await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);
		bool reservedB = await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userB,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		await Assert.That(value: reservedA).IsTrue();
		await Assert.That(value: reservedB).IsTrue();
	}

	[Test]
	public async Task TryReserveAsync_WithSameKeyDifferentCommandTypes_ShouldReserveBothIndependently()
	{
		Guid key = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();

		bool reservedTransaction = await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);
		bool reservedBudget = await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateBudgetCommand),
			userId: userId,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		await Assert.That(value: reservedTransaction).IsTrue();
		await Assert.That(value: reservedBudget).IsTrue();
	}

	[Test]
	public async Task TryReserveAsync_WithSameKeySameCommandTypeSameUser_ShouldOnlyReserveOnce()
	{
		Guid key = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();

		bool first = await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);
		bool second = await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		await Assert.That(value: first).IsTrue();
		await Assert.That(value: second).IsFalse();
	}

	[Test]
	public async Task GetAsync_WithSameKeyAndCommandTypeButDifferentUser_ShouldNotReturnOtherUsersEntry()
	{
		Guid key = Guid.CreateVersion7();
		Guid userA = Guid.CreateVersion7();
		Guid userB = Guid.CreateVersion7();
		Guid reservationId = Guid.CreateVersion7();

		await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA,
			reservationId: reservationId,
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);
		await _writeRepository.CompleteAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA,
			reservationId: reservationId,
			responseJson: """{"transactionId":"secret-belongs-to-user-a"}"""
		);

		IdempotencyEntry? entryForUserA = await _readRepository.GetAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA
		);
		IdempotencyEntry? entryForUserB = await _readRepository.GetAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userB
		);

		string? transactionId = JsonDocument.Parse(json: entryForUserA!.ResponseJson!).RootElement.GetProperty(propertyName: "transactionId").GetString();

		await Assert.That(value: entryForUserA).IsNotNull();
		await Assert.That(value: transactionId).IsEqualTo(expected: "secret-belongs-to-user-a");
		await Assert.That(value: entryForUserB).IsNull();
	}

	[Test]
	public async Task CompleteAsync_WithMatchingReservationId_ShouldUpdateAndReturnTrue()
	{
		Guid key = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid reservationId = Guid.CreateVersion7();

		await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: reservationId,
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		bool completed = await _writeRepository.CompleteAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: reservationId,
			responseJson: """{"ok":true}"""
		);

		await Assert.That(value: completed).IsTrue();
	}

	[Test]
	public async Task CompleteAsync_WithMismatchedReservationId_ShouldReturnFalseAndNotUpdate()
	{
		Guid key = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid originalReservationId = Guid.CreateVersion7();
		Guid staleReservationId = Guid.CreateVersion7();

		await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: originalReservationId,
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		bool completed = await _writeRepository.CompleteAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: staleReservationId,
			responseJson: """{"ok":true}"""
		);

		IdempotencyEntry? entry = await _readRepository.GetAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId
		);

		await Assert.That(value: completed).IsFalse();
		await Assert.That(value: entry!.ResponseJson).IsNull();
	}

	[Test]
	public async Task DeleteAsync_ShouldOnlyDeleteMatchingScope()
	{
		Guid key = Guid.CreateVersion7();
		Guid userA = Guid.CreateVersion7();
		Guid userB = Guid.CreateVersion7();
		Guid reservationIdA = Guid.CreateVersion7();

		await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA,
			reservationId: reservationIdA,
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);
		await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userB,
			reservationId: Guid.CreateVersion7(),
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		await _writeRepository.DeleteAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA,
			reservationId: reservationIdA
		);

		IdempotencyEntry? entryForUserA = await _readRepository.GetAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userA
		);
		IdempotencyEntry? entryForUserB = await _readRepository.GetAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userB
		);

		await Assert.That(value: entryForUserA).IsNull();
		await Assert.That(value: entryForUserB).IsNotNull();
	}

	[Test]
	public async Task DeleteAsync_WithMismatchedReservationId_ShouldReturnFalseAndNotDelete()
	{
		Guid key = Guid.CreateVersion7();
		Guid userId = Guid.CreateVersion7();
		Guid originalReservationId = Guid.CreateVersion7();
		Guid staleReservationId = Guid.CreateVersion7();

		await _writeRepository.TryReserveAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: originalReservationId,
			reservedAt: Now,
			expiresAt: Now.AddHours(hours: 1)
		);

		bool deleted = await _writeRepository.DeleteAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId,
			reservationId: staleReservationId
		);

		IdempotencyEntry? entry = await _readRepository.GetAsync(
			idempotencyKey: key,
			commandType: nameof(CreateTransactionCommand),
			userId: userId
		);

		await Assert.That(value: deleted).IsFalse();
		await Assert.That(value: entry).IsNotNull();
	}
}
