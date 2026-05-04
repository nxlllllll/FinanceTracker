using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Account;

public sealed class AccountReadRepositoryTests : DatabaseFixture
{
    private AccountReadRepository _readRepository = null!;
    private AccountWriteRepository _writeRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private AccountTypeBuilder _accountTypeBuilder = null!;
    private UserBuilder _userBuilder = null!;
    
    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new AccountReadRepository(context: Context);
        _writeRepository = new AccountWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
        _currencyBuilder = new CurrencyBuilder(context: Context);
        _accountTypeBuilder = new AccountTypeBuilder(context: Context);
        _userBuilder = new UserBuilder(context: Context);
    }

    private async Task<AccountCreated> CreateAccountAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

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

    private async Task<(Guid userId, AccountCreated @event)> CreateAccountWithArchivationAsync(bool archived = false)
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Карта Сбер",
            Type: accountType,
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
        await Assert.That(value: result!.Id).IsEqualTo(expected: @event.AccountId);
        await Assert.That(value: result.Name).IsEqualTo(expected: "Карта Сбер");
        await Assert.That(value: result.Balance).IsEqualTo(expected: 10000m);
        await Assert.That(value: result.IsArchived).IsFalse();
        await Assert.That(value: result.Type).IsEqualTo(expected: Core.Domains.Account.AccountType.Checking);
        await Assert.That(value: result.Currency).IsEqualTo(expected: "RUB");
    }

    [Test]
    public async Task GetAllAsync_WithNoAccounts_ShouldReturnEmptyList()
    {
        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: Guid.NewGuid());

        await Assert.That(value: result.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnOnlyUserAccounts()
    {
        (Guid userId, _) = await CreateAccountWithArchivationAsync();
        (_, _) = await CreateAccountWithArchivationAsync();

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].UserId).IsEqualTo(expected: userId);
    }

    [Test]
    public async Task GetAllAsync_WithIsArchivedFalse_ShouldReturnOnlyActiveAccounts()
    {
        (Guid userId, _) = await CreateAccountWithArchivationAsync(archived: false);
        (_, _) = await CreateAccountWithArchivationAsync(archived: true);

        IReadOnlyList<AccountDto> result = await _readRepository.GetAllAsync(
            userId: userId,
            isArchived: false
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsArchived).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithIsArchivedTrue_ShouldReturnOnlyArchivedAccounts()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        AccountCreated active = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Активный",
            Type: accountType,
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
            Type: accountType,
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
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        AccountCreated active = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Активный",
            Type: accountType,
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
            Type: accountType,
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