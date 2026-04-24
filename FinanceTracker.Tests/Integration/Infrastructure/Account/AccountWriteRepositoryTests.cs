using FinanceTracker.Core.Domains.Account;
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
        AccountType accountType = await CreateAccountTypeAsync();
        Guid userId = await CreateUserAsync(currencyCode: currencyCode);

        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Карта Сбер",
            Type: accountType,
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

        bool accountExists = await Context.Accounts.AnyAsync(predicate: a => a.Id == @event.AccountId);
        bool balanceExists = await Context.AccountBalances.AnyAsync(predicate: b => b.AccountId == @event.AccountId);

        await Assert.That(value: accountExists).IsTrue();
        await Assert.That(value: balanceExists).IsTrue();
    }

    [Test]
    public async Task RenameAsync_ShouldUpdateName()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.RenameAsync(accountId: created.AccountId, newName: "Карта Тинькофф");

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

        await _writeRepository.ArchiveAsync(accountId: created.AccountId);

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

        await _writeRepository.ArchiveAsync(accountId: created.AccountId);
        await _writeRepository.UnarchiveAsync(accountId: created.AccountId);

        bool isArchived = await Context.Accounts
            .Where(predicate: a => a.Id == created.AccountId)
            .Select(selector: a => a.IsArchived)
            .FirstAsync();

        await Assert.That(value: isArchived).IsFalse();
    }

    [Test]
    public async Task DebitAsync_ShouldDecreaseBalance()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.DebitAsync(@event: new AccountDebited(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        ));

        decimal balance = await Context.AccountBalances
            .Where(predicate: b => b.AccountId == created.AccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 9000m);
    }

    [Test]
    public async Task CreditAsync_ShouldIncreaseBalance()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.CreditAsync(@event: new AccountCredited(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 500m,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        ));

        decimal balance = await Context.AccountBalances
            .Where(predicate: b => b.AccountId == created.AccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 10500m);
    }

    [Test]
    public async Task DebitAsync_WithExchangeRate_ShouldApplyExchangeRate()
    {
        AccountCreated created = await CreateAccountAsync();

        await _writeRepository.DebitAsync(@event: new AccountDebited(
            Id: Guid.NewGuid(),
            AccountId: created.AccountId,
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 100m,
            ExchangeRate: 90m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        ));

        decimal balance = await Context.AccountBalances
            .Where(predicate: b => b.AccountId == created.AccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 1000m);
    }
}