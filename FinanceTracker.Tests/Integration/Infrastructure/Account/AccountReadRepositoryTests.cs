using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories.Account;

namespace FinanceTracker.Tests.Integration.Infrastructure.Account;

public sealed class AccountReadRepositoryTests : DatabaseFixture
{
    private AccountReadRepository _readRepository = null!;
    private AccountWriteRepository _writeRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new AccountReadRepository(context: Context);
        _writeRepository = new AccountWriteRepository(context: Context);
    }

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
    public async Task GetByIdAsync_WithNonExistentAccount_ShouldReturnNull()
    {
        AccountDto? result = await _readRepository.GetByIdAsync(accountId: Guid.NewGuid());
        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithExistingAccount_ShouldReturnCorrectDto()
    {
        AccountCreated @event = await CreateAccountAsync();

        AccountDto? result = await _readRepository.GetByIdAsync(accountId: @event.AccountId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result.Id).IsEqualTo(expected: @event.AccountId);
        await Assert.That(value: result.Name).IsEqualTo(expected: "Карта Сбер");
        await Assert.That(value: result.Balance).IsEqualTo(expected: 10000m);
        await Assert.That(value: result.IsArchived).IsFalse();
        await Assert.That(value: result.AccountType).IsEqualTo(expected: "checking");
        await Assert.That(value: result.Currency).IsEqualTo(expected: "RUB");
    }
}