using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Account;

public sealed class AccountReadRepositoryTests : DatabaseFixture
{
	private AccountReadRepository _readRepository = null!;
	private AccountWriteRepository _writeRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new AccountReadRepository(context: Context);
		_writeRepository = new AccountWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private async Task<AccountCreated> CreateAccountAsync()
	{
		Core.ValueObjects.Currency currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode.Value);

		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Карта Сбер").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 10000m,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await _writeRepository.CreateAsync(@event: @event);
		await Context.SaveChangesAsync();
		return @event;
	}

	private async Task<(Guid userId, AccountCreated @event)> CreateAccountWithArchivationAsync(bool archived = false)
	{
		Core.ValueObjects.Currency currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Карта Сбер").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 1000m,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await _writeRepository.CreateAsync(@event: @event);
		await Context.SaveChangesAsync();

		if (archived)
		{
			await _writeRepository.ArchiveAsync(@event: new AccountArchived(
				Id: Guid.CreateVersion7(),
				AccountId: @event.AccountId,
				OccurredAt: DateTimeOffset.UtcNow
			));
		}

		return (userId, @event);
	}

	[Test]
	public async Task GetByIdAsync_WithNonExistentAccount_ShouldReturnNull()
	{
		AccountReadModel? result = await _readRepository.GetByIdAsync(accountId: Guid.CreateVersion7(), userId: Guid.CreateVersion7());
		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WithExistingAccount_ShouldReturnCorrectReadModel()
	{
		AccountCreated @event = await CreateAccountAsync();

		AccountReadModel? result = await _readRepository.GetByIdAsync(accountId: @event.AccountId, userId: @event.UserId);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: @event.AccountId);
		await Assert.That(value: result.Name.Value).IsEqualTo(expected: "Карта Сбер");
		await Assert.That(value: result.Balance.Amount).IsEqualTo(expected: 10000m);
		await Assert.That(value: result.IsArchived).IsFalse();
		await Assert.That(value: result.Type).IsEqualTo(expected: AccountType.Checking);
		await Assert.That(value: result.Balance.Currency.Value).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task GetAllAsync_WithNoAccounts_ShouldReturnEmptyList()
	{
		IReadOnlyList<AccountReadModel> result = await _readRepository.GetAllAsync(userId: Guid.CreateVersion7());
		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnOnlyUserAccounts()
	{
		(Guid userId, _) = await CreateAccountWithArchivationAsync();
		await CreateAccountWithArchivationAsync();

		IReadOnlyList<AccountReadModel> result = await _readRepository.GetAllAsync(userId: userId);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task GetAllAsync_WithIsArchivedFalse_ShouldReturnOnlyActiveAccounts()
	{
		(Guid userId, _) = await CreateAccountWithArchivationAsync(archived: false);
		await CreateAccountWithArchivationAsync(archived: true);

		IReadOnlyList<AccountReadModel> result = await _readRepository.GetAllAsync(userId: userId, isArchived: false);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].IsArchived).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithIsArchivedTrue_ShouldReturnOnlyArchivedAccounts()
	{
		Core.ValueObjects.Currency currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

		AccountCreated active = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Активный").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 1000m,
			OccurredAt: DateTimeOffset.UtcNow
		);
		await _writeRepository.CreateAsync(@event: active);

		AccountCreated archived = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Архивированный").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 500m,
			OccurredAt: DateTimeOffset.UtcNow
		);
		await _writeRepository.CreateAsync(@event: archived);
		await Context.SaveChangesAsync();

		await _writeRepository.ArchiveAsync(@event: new AccountArchived(
			Id: Guid.CreateVersion7(),
			AccountId: archived.AccountId,
			OccurredAt: DateTimeOffset.UtcNow
		));

		IReadOnlyList<AccountReadModel> result = await _readRepository.GetAllAsync(userId: userId, isArchived: true);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].IsArchived).IsTrue();
	}

	[Test]
	public async Task GetAllAsync_WithNullIsArchived_ShouldReturnAllAccounts()
	{
		Core.ValueObjects.Currency currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

		AccountCreated active = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Активный").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 1000m,
			OccurredAt: DateTimeOffset.UtcNow
		);
		await _writeRepository.CreateAsync(@event: active);

		AccountCreated archived = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Архивированный").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 500m,
			OccurredAt: DateTimeOffset.UtcNow
		);
		await _writeRepository.CreateAsync(@event: archived);
		await Context.SaveChangesAsync();

		await _writeRepository.ArchiveAsync(@event: new AccountArchived(
			Id: Guid.CreateVersion7(),
			AccountId: archived.AccountId,
			OccurredAt: DateTimeOffset.UtcNow
		));

		IReadOnlyList<AccountReadModel> result = await _readRepository.GetAllAsync(userId: userId, isArchived: null);

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByIdAsync_WithWrongUserId_ShouldReturnNull()
	{
		AccountCreated @event = await CreateAccountAsync();

		AccountReadModel? result = await _readRepository.GetByIdAsync(accountId: @event.AccountId, userId: Guid.CreateVersion7());

		await Assert.That(value: result).IsNull();
	}
}