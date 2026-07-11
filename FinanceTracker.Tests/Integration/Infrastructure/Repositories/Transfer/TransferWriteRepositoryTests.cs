using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
			isRatePending: false,
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
		await Assert.That(value: entity.IsRatePending).IsFalse();
		await Assert.That(value: entity.Description).IsEqualTo(expected: "Test transfer");
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 0);
		await Assert.That(value: entity.CreatedAt).IsEqualTo(expected: transfer.CreatedAt);
	}

	[Test]
	public async Task UpdateStatusAsync_ShouldUpdateStatus()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();

		await _writeRepository.UpdateStatusAsync(
			transferId: transfer.Id,
			userId: transfer.UserId,
			status: TransferStatus.Completed,
			expectedVersion: 0
		);

		TransferEntity entity = await Context.Transfers.AsNoTracking().FirstAsync(predicate: t => t.Id == transfer.Id);

		await Assert.That(value: entity.Status).IsEqualTo(expected: TransferStatus.Completed);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task UpdateStatusAsync_WhenVersionConflict_ShouldThrowConcurrencyConflictException()
	{
		Core.Domains.Transfer.Transfer transfer = await CreateAndSaveTransferAsync();

		await _writeRepository.UpdateStatusAsync(
			transferId: transfer.Id,
			userId: transfer.UserId,
			status: TransferStatus.Completed,
			expectedVersion: 0
		);

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await _writeRepository.UpdateStatusAsync(
			transferId: transfer.Id,
			userId: transfer.UserId,
			status: TransferStatus.Compensated,
			expectedVersion: 0
		));
	}
}
