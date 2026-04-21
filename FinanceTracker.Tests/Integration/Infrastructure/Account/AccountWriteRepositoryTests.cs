using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Account;

public sealed class AccountWriteRepositoryTests : DatabaseFixture
{
    private AccountWriteRepository _writeRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
        => _writeRepository = new AccountWriteRepository(context: Context);

    private async Task<AccountCreated> CreateAccountAsync()
    {
        string currencyCode = await CreateCurrencyAsync();
        string accountType = await CreateAccountTypeAsync();
        Guid userId = await CreateUserAsync(currencyCode: currencyCode);

        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Карта Сбер",
            AccountType: accountType,
            Currency: currencyCode,
            Balance: 10000m,
            OccurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(@event: @event);
        return @event;
    }

    [Test]
    public async Task CreateAsync_ShouldCreateAccountAndBalance()
    {
        AccountCreated @event = await CreateAccountAsync();

        bool accountExists = await Context.Accounts.AnyAsync(
            predicate: a => a.Id == @event.AccountId
        );
        bool balanceExists = await Context.AccountBalances.AnyAsync(
            predicate: b => b.AccountId == @event.AccountId
        );

        await Assert.That(value: accountExists).IsTrue();
        await Assert.That(value: balanceExists).IsTrue();
    }

    [Test]
    public async Task RenameAsync_ShouldUpdateName()
    {
        AccountCreated created = await CreateAccountAsync();

        AccountRenamed renamed = new AccountRenamed(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            NewName: "Карта Тинькофф",
            OccurredAt: DateTime.UtcNow
        );
        await _writeRepository.RenameAsync(@event: renamed);

        string? name = await Context.Accounts
            .Where(predicate: a => a.Id == created.AccountId)
            .Select(selector: a => a.Name)
            .FirstOrDefaultAsync();

        await Assert.That(value: name).IsEqualTo(expected: "Карта Тинькофф");
    }

    [Test]
    public async Task ArchiveAsync_ShouldSetIsArchivedTrue()
    {
        AccountCreated created = await CreateAccountAsync();

        AccountArchived archived = new AccountArchived(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            OccurredAt: DateTime.UtcNow
        );
        await _writeRepository.ArchiveAsync(@event: archived);

        bool isArchived = await Context.Accounts
            .Where(predicate: a => a.Id == created.AccountId)
            .Select(selector: a => a.IsArchived)
            .FirstAsync();

        await Assert.That(value: isArchived).IsTrue();
    }

    [Test]
    public async Task UnarchiveAsync_ShouldSetIsArchivedFalse()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.ArchiveAsync(@event: new AccountArchived(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            OccurredAt: DateTime.UtcNow
        ));

        await _writeRepository.UnarchiveAsync(@event: new AccountUnarchived(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            OccurredAt: DateTime.UtcNow
        ));

        bool isArchived = await Context.Accounts
            .Where(predicate: a => a.Id == created.AccountId)
            .Select(selector: a => a.IsArchived)
            .FirstAsync();

        await Assert.That(value: isArchived).IsFalse();
    }

    [Test]
    public async Task UpdateBalanceAsync_WithDebit_ShouldDecreaseBalance()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.UpdateBalanceAsync(
            accountId: created.AccountId,
            amount: -1000m
        );

        decimal balance = await Context.AccountBalances
            .Where(predicate: b => b.AccountId == created.AccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 9000m);
    }

    [Test]
    public async Task UpdateBalanceAsync_WithCredit_ShouldIncreaseBalance()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.UpdateBalanceAsync(
            accountId: created.AccountId,
            amount: 500m
        );

        decimal balance = await Context.AccountBalances
            .Where(predicate: b => b.AccountId == created.AccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 10500m);
    }
}