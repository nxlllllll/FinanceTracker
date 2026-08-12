using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Infrastructure.Database.Context.Transfer;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Transfer;

public sealed class TransferWriteRepositoryTests : DatabaseFixture
{
	private TransferWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;

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
	public async Task CreateAsync_ShouldPersistTransfer()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();

		TransferEntity? entity = await Context.Transfers.FirstOrDefaultAsync(predicate: t => t.Id == transfer.Id);

		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.AmountFrom).IsEqualTo(expected: 1000m);
		await Assert.That(value: entity.AmountTo).IsEqualTo(expected: 900m);
		await Assert.That(value: entity.ExchangeRate).IsEqualTo(expected: 0.9m);
		await Assert.That(value: entity.RateStatus).IsEqualTo(expected: RateStatus.Exact);
		await Assert.That(value: entity.Description).IsEqualTo(expected: "Test transfer");
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 0);
		await Assert.That(value: entity.CreatedAt).IsEqualTo(expected: transfer.CreatedAt);
	}

	[Test]
	public async Task SaveStatusAsync_ShouldUpdateStatus()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();
		transfer.Complete();

		await _writeRepository.SaveStatusAsync(transfer: transfer);

		TransferEntity entity = await Context.Transfers.AsNoTracking().FirstAsync(predicate: t => t.Id == transfer.Id);

		await Assert.That(value: entity.Status).IsEqualTo(expected: TransferStatus.Completed);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task SaveStatusAsync_WhenCompensatingAPendingRate_ShouldPersistTheCancelledRateTogetherWithTheStatus()
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
			currencyFrom: Core.ValueObjects.Currency.Create(value: "USD").Value,
			currencyTo: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending,
			description: null,
			occurredAt: DateTimeOffset.UtcNow
		).Value!;

		await _writeRepository.CreateAsync(transfer: transfer);
		await Context.SaveChangesAsync();

		transfer.Compensate(occurredAt: DateTimeOffset.UtcNow);
		await _writeRepository.SaveStatusAsync(transfer: transfer);

		TransferEntity entity = await Context.Transfers.AsNoTracking().FirstAsync(predicate: t => t.Id == transfer.Id);

		await Assert.That(value: entity.Status).IsEqualTo(expected: TransferStatus.Compensated);
		await Assert.That(value: entity.RateStatus).IsEqualTo(expected: RateStatus.Cancelled);
	}

	[Test]
	public async Task SaveStatusAsync_WhenVersionConflict_ShouldThrowConcurrencyConflictException()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();

		Core.Domains.Transfer.Transfer staleCopy = Core.Domains.Transfer.Transfer.Reconstitute(
			id: transfer.Id,
			userId: transfer.UserId,
			fromAccountId: transfer.FromAccountId,
			toAccountId: transfer.ToAccountId,
			amountFrom: transfer.AmountFrom,
			amountTo: transfer.AmountTo,
			exchangeRate: transfer.ExchangeRate,
			rateStatus: transfer.RateStatus,
			rateStatusChangedAt: transfer.RateStatusChangedAt,
			status: transfer.Status,
			description: transfer.Description,
			rowVersion: transfer.RowVersion,
			occurredAt: transfer.OccurredAt,
			createdAt: transfer.CreatedAt
		);

		transfer.Complete();
		await _writeRepository.SaveStatusAsync(transfer: transfer);

		staleCopy.Fail(occurredAt: DateTimeOffset.UtcNow);

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await _writeRepository.SaveStatusAsync(transfer: staleCopy));
	}
}
