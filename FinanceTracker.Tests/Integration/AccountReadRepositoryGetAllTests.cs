using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Tests.Integration.Infrastructure;

namespace FinanceTracker.Tests.Integration;

public sealed class AccountReadRepositoryGetAllTests : DatabaseFixture
{
    private AccountReadRepository _readRepository = null!;
    private AccountWriteRepository _writeRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new AccountReadRepository(context: Context);
        _writeRepository = new AccountWriteRepository(context: Context);
    }

    private async Task<(Guid userId, AccountCreated @event)> CreateAccountAsync(bool archived = false)
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
            Balance: 1000m,
            OccurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(@event: @event);

        if (archived)
        {
            await _writeRepository.ArchiveAsync(@event: new AccountArchived(
                Id: Guid.NewGuid(),
                AccountId: @event.AccountId,
                OccurredAt: DateTime.UtcNow
            ));
        }

        return (userId, @event);
    }

    [Test]
    public async Task GetAllAsync_WithNoAccounts_ShouldReturnEmptyList()
    {
        Guid userId = Guid.NewGuid();

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnOnlyUserAccounts()
    {
        (Guid userId, _) = await CreateAccountAsync();
        (_, _) = await CreateAccountAsync();

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].UserId).IsEqualTo(expected: userId);
    }

    [Test]
    public async Task GetAllAsync_WithIsArchivedFalse_ShouldReturnOnlyActiveAccounts()
    {
        (Guid userId, _) = await CreateAccountAsync(archived: false);
        (_, _) = await CreateAccountAsync(archived: true);

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: userId, isArchived: false);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsArchived).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithIsArchivedTrue_ShouldReturnOnlyArchivedAccounts()
    {
        string currencyCode = await CreateCurrencyAsync();
        string accountType = await CreateAccountTypeAsync();
        Guid userId = await CreateUserAsync(currencyCode: currencyCode);
        
        AccountCreated active = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Активный",
            AccountType: accountType,
            Currency: currencyCode,
            Balance: 1000m,
            OccurredAt: DateTime.UtcNow
        );
        await _writeRepository.CreateAsync(@event: active);

        AccountCreated archived = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Заархивированный",
            AccountType: accountType,
            Currency: currencyCode,
            Balance: 500m,
            OccurredAt: DateTime.UtcNow
        );
        await _writeRepository.CreateAsync(@event: archived);
        await _writeRepository.ArchiveAsync(@event: new AccountArchived(
            Id: Guid.NewGuid(),
            AccountId: archived.AccountId,
            OccurredAt: DateTime.UtcNow
        ));

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: userId, isArchived: true);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsArchived).IsTrue();
    }

    [Test]
    public async Task GetAllAsync_WithNullIsArchived_ShouldReturnAllAccounts()
    {
        string currencyCode = await CreateCurrencyAsync();
        string accountType = await CreateAccountTypeAsync();
        Guid userId = await CreateUserAsync(currencyCode: currencyCode);

        AccountCreated active = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Активный",
            AccountType: accountType,
            Currency: currencyCode,
            Balance: 1000m,
            OccurredAt: DateTime.UtcNow
        );
        await _writeRepository.CreateAsync(@event: active);

        AccountCreated archived = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Заархивированный",
            AccountType: accountType,
            Currency: currencyCode,
            Balance: 500m,
            OccurredAt: DateTime.UtcNow
        );
        await _writeRepository.CreateAsync(@event: archived);
        await _writeRepository.ArchiveAsync(@event: new AccountArchived(
            Id: Guid.NewGuid(),
            AccountId: archived.AccountId,
            OccurredAt: DateTime.UtcNow
        ));

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: userId, isArchived: null);

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }
}