using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Account;

public sealed class AccountWriteRepositoryTests : DatabaseFixture
{
	private AccountWriteRepository _writeRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new AccountWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private async Task<AccountCreated> CreateAccountAsync()
	{
		Core.ValueObjects.Currency currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: currencyCode,
			Balance: 10000m,
			Version: 1,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await _writeRepository.CreateAsync(@event: @event);
		await Context.SaveChangesAsync();
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

		await _writeRepository.RenameAsync(@event: new AccountRenamed(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			NewName: Name.Create(value: "Новое название").Value,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		string name = await Context.Accounts
			.Where(predicate: a => a.Id == created.AccountId)
			.Select(selector: a => a.Name)
			.FirstOrDefaultAsync();

		await Assert.That(value: name).IsEqualTo(expected: "Новое название");
	}

	[Test]
	public async Task ArchiveAsync_ShouldSetIsArchivedTrue()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.ArchiveAsync(@event: new AccountArchived(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

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
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));
		await _writeRepository.UnarchiveAsync(@event: new AccountUnarchived(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			Version: 3,
			OccurredAt: DateTimeOffset.UtcNow
		));

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
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
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
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
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
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 100m,
			ExchangeRate: 90m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		decimal balance = await Context.AccountBalances
			.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task TransferDebitAsync_ShouldDecreaseBalance()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.TransferDebitAsync(@event: new AccountTransferDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransferId: Guid.CreateVersion7(),
			ToAccountId: Guid.CreateVersion7(),
			Amount: 3000m,
			ForexRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		decimal balance = await Context.AccountBalances
			.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 7000m);
	}

	[Test]
	public async Task TransferCreditAsync_WithExchangeRate_ShouldIncreaseBalanceByConvertedAmount()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.TransferCreditAsync(@event: new AccountTransferCredited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransferId: Guid.CreateVersion7(),
			FromAccountId: Guid.CreateVersion7(),
			Amount: 100m,
			ExchangeRate: 90m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		decimal balance = await Context.AccountBalances
			.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 19000m);
	}

	[Test]
	public async Task RefundTransferAsync_ShouldIncreaseBalance()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.RefundTransferAsync(@event: new AccountTransferRefunded(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransferId: Guid.CreateVersion7(),
			Amount: 2500m,
			Description: "Refund: ToAccount not found.",
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		decimal balance = await Context.AccountBalances
			.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 12500m);
	}

	[Test]
	public async Task AdjustBalanceAsync_WithPositiveDelta_ShouldIncreaseBalance()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.AdjustBalanceAsync(@event: new AccountBalanceAdjusted(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			SourceId: Guid.CreateVersion7(),
			SourceType: AggregateTypeNames.Transaction,
			OldRate: 85m,
			NewRate: 90m,
			Amount: 1000m,
			Delta: 5000m,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		decimal balance = await Context.AccountBalances
			.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 15000m);
	}

	[Test]
	public async Task AdjustBalanceAsync_WithNegativeDelta_ShouldDecreaseBalance()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.AdjustBalanceAsync(@event: new AccountBalanceAdjusted(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			SourceId: Guid.CreateVersion7(),
			SourceType: AggregateTypeNames.Transaction,
			OldRate: 90m,
			NewRate: 85m,
			Amount: 1000m,
			Delta: -5000m,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		decimal balance = await Context.AccountBalances
			.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 5000m);
	}

	[Test]
	public async Task DebitAsync_WithSequentialVersions_ShouldApplyAllAndUpdateBalance()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));
		await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 2000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 3,
			OccurredAt: DateTimeOffset.UtcNow
		));
		await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 3000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 4,
			OccurredAt: DateTimeOffset.UtcNow
		));

		var result = await Context.AccountBalances.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => new { b.Balance, b.LastVersion })
			.FirstAsync();

		await Assert.That(value: result.Balance).IsEqualTo(expected: 4000m);
		await Assert.That(value: result.LastVersion).IsEqualTo(expected: 4);
	}

	[Test]
	public async Task DebitAsync_WithDuplicateVersion_ShouldThrowConcurrencyConflictException()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		await Assert.That(async () => await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		))).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task DebitAsync_WithStaleVersion_ShouldThrowConcurrencyConflictException()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		await Assert.That(async () => await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			Version: 1,
			OccurredAt: DateTimeOffset.UtcNow
		))).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task DebitAsync_WhenRetriedWithSameVersion_ShouldNotApplyTwice()
	{
		AccountCreated created = await CreateAccountAsync();

		AccountDebited @event = new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		);

		await _writeRepository.DebitAsync(@event: @event);

		await Assert.That(async () => await _writeRepository.DebitAsync(@event: @event)).Throws<ConcurrencyConflictException>();

		decimal balance = await Context.AccountBalances.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task CreditAsync_WithDuplicateVersion_ShouldThrowConcurrencyConflictException()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.CreditAsync(@event: new AccountCredited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		await Assert.That(async () => await _writeRepository.CreditAsync(@event: new AccountCredited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		))).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task AdjustBalanceAsync_WithDuplicateVersion_ShouldThrowConcurrencyConflictException()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.AdjustBalanceAsync(@event: new AccountBalanceAdjusted(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			SourceId: Guid.CreateVersion7(),
			SourceType: AggregateTypeNames.Transaction,
			OldRate: 85m,
			NewRate: 90m,
			Amount: 1000m,
			Delta: 5000m,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		await Assert.That(async () => await _writeRepository.AdjustBalanceAsync(@event: new AccountBalanceAdjusted(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			SourceId: Guid.CreateVersion7(),
			SourceType: AggregateTypeNames.Transaction,
			OldRate: 85m,
			NewRate: 90m,
			Amount: 1000m,
			Delta: 5000m,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		))).Throws<ConcurrencyConflictException>();
	}

	[Test]
	public async Task DebitAsync_WithDuplicateVersion_ShouldNotChangeBalance()
	{
		AccountCreated created = await CreateAccountAsync();

		await _writeRepository.DebitAsync(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: created.AccountId,
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 1m,
			Description: null,
			Version: 2,
			OccurredAt: DateTimeOffset.UtcNow
		));

		try
		{
			await _writeRepository.DebitAsync(@event: new AccountDebited(
				Id: Guid.CreateVersion7(),
				AccountId: created.AccountId,
				TransactionId: Guid.CreateVersion7(),
				CategoryId: Guid.CreateVersion7(),
				Amount: 999m,
				ExchangeRate: 1m,
				Description: null,
				Version: 2,
				OccurredAt: DateTimeOffset.UtcNow
			));
		}
		catch (ConcurrencyConflictException) { }

		decimal balance = await Context.AccountBalances.Where(predicate: b => b.AccountId == created.AccountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 9000m);
	}
}