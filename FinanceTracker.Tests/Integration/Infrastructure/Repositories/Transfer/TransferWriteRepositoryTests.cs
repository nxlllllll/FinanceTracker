using FinanceTracker.Infrastructure.Database.Context.Transfer;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;
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
		_writeRepository = new TransferWriteRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
	}

	[Test]
	public async Task CreateAsync_ShouldPersistTransfer()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid fromAccountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid toAccountId = await _accountBuilder.CreateAsync(userId: userId);

		Core.Domains.Transfer.Transfer transfer = Core.Domains.Transfer.Transfer.Create(
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

		TransferEntity? entity = await Context.Transfers.FirstOrDefaultAsync(predicate: t => t.Id == transfer.Id);

		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: entity.AmountFrom).IsEqualTo(expected: 1000m);
		await Assert.That(value: entity.AmountTo).IsEqualTo(expected: 900m);
		await Assert.That(value: entity.ExchangeRate).IsEqualTo(expected: 0.9m);
		await Assert.That(value: entity.IsRatePending).IsFalse();
		await Assert.That(value: entity.Description).IsEqualTo(expected: "Test transfer");
	}
}