using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Infrastructure.Database.Repositories.Transfers;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transfer;

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
        Core.Domains.Transfer.Transfer? result = await _readRepository.GetByIdAsync(transferId: Guid.NewGuid());
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
            toAccountId: toAccountId,
            amountFrom: 1000m,
            amountTo: 900m
        );

        Core.Domains.Transfer.Transfer? result = await _readRepository.GetByIdAsync(transferId: transferId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: transferId);
        await Assert.That(value: result.UserId).IsEqualTo(expected: userId);
        await Assert.That(value: result.AmountFrom).IsEqualTo(expected: 1000m);
        await Assert.That(value: result.AmountTo).IsEqualTo(expected: 900m);
        await Assert.That(value: result.IsExcluded).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithNoTransfers_ShouldReturnEmptyList()
    {
        IReadOnlyList<Core.Domains.Transfer.Transfer> result = await _readRepository.GetAllAsync(userId: Guid.NewGuid());
        await Assert.That(value: result.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnOnlyUserTransfers()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid anotherUserId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid anotherAccountId = await _accountBuilder.CreateAsync(userId: anotherUserId);

        await _transferBuilder.CreateAsync(userId: userId, fromAccountId: accountId, toAccountId: accountId);
        await _transferBuilder.CreateAsync(userId: anotherUserId, fromAccountId: anotherAccountId, toAccountId: anotherAccountId);

        IReadOnlyList<Core.Domains.Transfer.Transfer> result = await _readRepository.GetAllAsync(userId: userId);

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

        await _transferBuilder.CreateAsync(userId: userId, fromAccountId: accountA, toAccountId: accountB);
        await _transferBuilder.CreateAsync(userId: userId, fromAccountId: accountC, toAccountId: accountC);

        IReadOnlyList<Core.Domains.Transfer.Transfer> result = await _readRepository.GetAllAsync(userId: userId, accountId: accountA);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].FromAccountId).IsEqualTo(expected: accountA);
    }

    [Test]
    public async Task GetAllAsync_WithDateFilter_ShouldReturnOnlyMatchingTransfers()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);

        DateTime old = DateTime.UtcNow.AddDays(-10);
        DateTime recent = DateTime.UtcNow;

        await _transferBuilder.CreateAsync(userId: userId, fromAccountId: accountId, toAccountId: accountId, occurredAt: old);
        await _transferBuilder.CreateAsync(userId: userId, fromAccountId: accountId, toAccountId: accountId, occurredAt: recent);

        IReadOnlyList<Core.Domains.Transfer.Transfer> result = await _readRepository.GetAllAsync(
            userId: userId,
            dateFrom: DateTime.UtcNow.AddDays(-1)
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
    }
}