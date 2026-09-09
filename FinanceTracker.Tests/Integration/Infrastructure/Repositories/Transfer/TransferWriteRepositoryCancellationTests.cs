using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Transfer;

public sealed class TransferWriteRepositoryCancellationTests : DatabaseFixture
{
	private TransferWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;

	private static readonly TimeSpan Window = TimeSpan.FromDays(value: 30);

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new TransferWriteRepository(context: Context, operationRepository: new OperationWriteRepository(context: Context));
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
	}

	private async Task<Core.Domains.Transfer.Transfer> CreateAndSaveTransferAsync()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid fromAccountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid toAccountId = await _accountBuilder.CreateAsync(userId: userId);

		Core.Domains.Transfer.Transfer transfer = Core.Domains.Transfer.Transfer.Create(
			createdAt: DateTimeOffset.UtcNow,
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amount: 1000m,
			currencyFrom: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			currencyTo: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			exchangeRate: 0.9m,
			rateStatus: RateStatus.Exact,
			description: "Test transfer",
			occurredAt: DateTimeOffset.UtcNow
		).Value!;

		await _writeRepository.CreateAsync(transfer: transfer);
		await Context.SaveChangesAsync();
		return transfer;
	}

	[Test]
	public async Task CancelAsync_ShouldPersistTheCancelledStatus()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();
		transfer.Cancel(cancelledAt: DateTimeOffset.UtcNow, maxAge: Window);

		await _writeRepository.CancelAsync(transfer: transfer, reversalId: Guid.CreateVersion7(), occurredAt: DateTimeOffset.UtcNow);
		await Context.SaveChangesAsync();

		TransferEntity entity = await Context.Transfers.AsNoTracking().FirstAsync(predicate: t => t.Id == transfer.Id);

		await Assert.That(value: entity.Status).IsEqualTo(expected: TransferStatus.Cancelled).Because(message: """
			rm_transfers.status carries a foreign key to transfer_statuses, so a status the seed does not
			know about fails on write rather than on read. This is what the V061 row exists for.
		""");

		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task CancelAsync_ShouldFlagTheOriginalOperationAsReverted()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();
		transfer.Cancel(cancelledAt: DateTimeOffset.UtcNow, maxAge: Window);

		await _writeRepository.CancelAsync(transfer: transfer, reversalId: Guid.CreateVersion7(), occurredAt: DateTimeOffset.UtcNow);
		await Context.SaveChangesAsync();

		OperationEntity original = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == transfer.Id);

		await Assert.That(value: original.IsReverted).IsTrue();
	}

	[Test]
	public async Task CancelAsync_ShouldAddAReversalPointingBackAtTheOriginal()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();
		transfer.Cancel(cancelledAt: DateTimeOffset.UtcNow, maxAge: Window);

		Guid reversalId = Guid.CreateVersion7();
		DateTimeOffset occurredAt = DateTimeOffset.UtcNow;

		await _writeRepository.CancelAsync(transfer: transfer, reversalId: reversalId, occurredAt: occurredAt);
		await Context.SaveChangesAsync();

		OperationEntity reversal = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == reversalId);

		await Assert.That(value: reversal.ReversalOfId).IsEqualTo(expected: transfer.Id);
		await Assert.That(value: reversal.IsReverted).IsFalse();
	}

	[Test]
	public async Task CancelAsync_ShouldRecordTheReversalRunningTheOtherWay()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();
		transfer.Cancel(cancelledAt: DateTimeOffset.UtcNow, maxAge: Window);

		Guid reversalId = Guid.CreateVersion7();

		await _writeRepository.CancelAsync(transfer: transfer, reversalId: reversalId, occurredAt: DateTimeOffset.UtcNow);
		await Context.SaveChangesAsync();

		OperationEntity reversal = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == reversalId);

		await Assert.That(value: reversal.FromAccountId).IsEqualTo(expected: transfer.ToAccountId).Because(message: """
			A reversal is the same movement in the opposite direction. Copying the original's sides would
			read as a second transfer along the same route rather than as its undoing.
		""");

		await Assert.That(value: reversal.ToAccountId).IsEqualTo(expected: transfer.FromAccountId);
		await Assert.That(value: reversal.AmountFrom).IsEqualTo(expected: transfer.AmountTo.Amount);
		await Assert.That(value: reversal.AmountTo).IsEqualTo(expected: transfer.AmountFrom.Amount);
	}

	[Test]
	public async Task CancelAsync_OnAStaleVersion_ShouldRefuse()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();
		transfer.Complete();

		await _writeRepository.SaveStatusAsync(transfer: transfer);
		await Context.SaveChangesAsync();

		Core.Domains.Transfer.Transfer stale = Core.Domains.Transfer.Transfer.Reconstitute(
			id: transfer.Id,
			userId: transfer.UserId,
			fromAccountId: transfer.FromAccountId,
			toAccountId: transfer.ToAccountId,
			amountFrom: transfer.AmountFrom,
			amountTo: transfer.AmountTo,
			exchangeRate: transfer.ExchangeRate,
			rateStatus: transfer.RateStatus,
			rateStatusChangedAt: transfer.RateStatusChangedAt,
			status: TransferStatus.Completed,
			description: transfer.Description,
			rowVersion: 0,
			occurredAt: transfer.OccurredAt,
			createdAt: transfer.CreatedAt
		);

		stale.Cancel(cancelledAt: DateTimeOffset.UtcNow, maxAge: Window);

		await Assert.That(async () => await _writeRepository.CancelAsync(
			transfer: stale,
			reversalId: Guid.CreateVersion7(),
			occurredAt: DateTimeOffset.UtcNow
		)).Throws<ConcurrencyConflictException>().Because(message: """
			The projection worker completes a transfer from the same starting state a user cancels it from.
			Without the version check both would apply, crediting the destination and refunding the source
			for one and the same movement.
		""");
	}
}
