using FinanceTracker.Core.ReadModels;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Transfer;

public sealed class TransferReadRepositoryTests : DatabaseFixture
{
	private TransferReadRepository _readRepository = null!;
	private TransferBuilder _transferBuilder = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new TransferReadRepository(context: Context);
		_transferBuilder = new TransferBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
	}

	[Test]
	public async Task GetByIdAsync_WithNonExistentTransfer_ShouldReturnNull()
	{
		TransferReadModel? result = await _readRepository.GetByIdAsync(transferId: Guid.CreateVersion7());
		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WithExistingTransfer_ShouldReturnCorrectTransfer()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid fromAccountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid toAccountId = await _accountBuilder.CreateAsync(userId: userId);

		Guid transferId = await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: fromAccountId,
			currencyFrom: "RUB",
			toAccountId: toAccountId,
			currencyTo: "RUB",
			amount: 1000m,
			exchangeRate: 0.9m
		);

		TransferReadModel? result = await _readRepository.GetByIdAsync(transferId: transferId);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: transferId);
		await Assert.That(value: result.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: result.AmountFrom.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: result.AmountTo.Amount).IsEqualTo(expected: 900m);
	}

	[Test]
	public async Task GetAllAsync_WithNoTransfers_ShouldReturnEmptyList()
	{
		IReadOnlyList<TransferReadModel> result = await _readRepository.GetAllAsync(userId: Guid.CreateVersion7());
		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnOnlyUserTransfers()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid anotherUserId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid anotherAccountId = await _accountBuilder.CreateAsync(userId: anotherUserId);
		Guid anotherAccountId2 = await _accountBuilder.CreateAsync(userId: anotherUserId);

		await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: accountId,
			currencyFrom: "RUB",
			toAccountId: await _accountBuilder.CreateAsync(userId: userId),
			currencyTo: "RUB"
		);
		await _transferBuilder.CreateAsync(
			userId: anotherUserId,
			fromAccountId: anotherAccountId,
			currencyFrom: "RUB",
			toAccountId: anotherAccountId2,
			currencyTo: "RUB"
		);

		IReadOnlyList<TransferReadModel> result = await _readRepository.GetAllAsync(userId: userId);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task GetAllAsync_WithAccountIdFilter_ShouldReturnTransfersForAccount()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountA = await _accountBuilder.CreateAsync(userId: userId);
		Guid accountB = await _accountBuilder.CreateAsync(userId: userId);
		Guid accountC = await _accountBuilder.CreateAsync(userId: userId);
		Guid accountD = await _accountBuilder.CreateAsync(userId: userId);

		await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: accountA,
			currencyFrom: "RUB",
			toAccountId: accountB,
			currencyTo: "RUB"
		);
		await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: accountC,
			currencyFrom: "RUB",
			toAccountId: accountD,
			currencyTo: "RUB"
		);

		IReadOnlyList<TransferReadModel> result = await _readRepository.GetAllAsync(userId: userId, accountId: accountA);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].FromAccountId).IsEqualTo(expected: accountA);
	}

	[Test]
	public async Task GetAllAsync_WithDateFilter_ShouldReturnOnlyMatchingTransfers()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountA = await _accountBuilder.CreateAsync(userId: userId);
		Guid accountB = await _accountBuilder.CreateAsync(userId: userId);

		DateTimeOffset old = DateTimeOffset.UtcNow.AddDays(days: -10);
		DateTimeOffset recent = DateTimeOffset.UtcNow;

		await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: accountA,
			currencyFrom: "RUB",
			toAccountId: accountB,
			currencyTo: "RUB",
			occurredAt: old
		);
		await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: accountA,
			currencyFrom: "RUB",
			toAccountId: accountB,
			currencyTo: "RUB",
			occurredAt: recent
		);

		IReadOnlyList<TransferReadModel> result = await _readRepository.GetAllAsync(
			userId: userId,
			dateFrom: DateTimeOffset.UtcNow.AddDays(days: -1)
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}
}