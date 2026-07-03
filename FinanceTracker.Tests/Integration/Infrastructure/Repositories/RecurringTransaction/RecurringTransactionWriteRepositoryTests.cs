using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.RecurringTransaction;

public sealed class RecurringTransactionWriteRepositoryTests : DatabaseFixture
{
	private RecurringTransactionWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new RecurringTransactionWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
	}

	private async Task<Core.Domains.RecurringTransaction.RecurringTransaction> CreateRecurringTransactionAsync(
		Guid userId,
		Guid accountId,
		Guid categoryId,
		decimal amount = 5000m,
		int dayOfMonth = 15)
	{
		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: amount, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: dayOfMonth,
			description: null
		);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);
		await Context.SaveChangesAsync();
		return recurringTransaction;
	}

	[Test]
	public async Task CreateAsync_ShouldCreateRecurringTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		RecurringTransactionEntity? entity = await Context.RecurringTransactions.FirstOrDefaultAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.Amount).IsEqualTo(expected: 5000m);
		await Assert.That(value: entity.DayOfMonth).IsEqualTo(expected: 15);
		await Assert.That(value: entity.IsActive).IsTrue();
		await Assert.That(value: entity.LastExecutedAt).IsNull();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task ChangeAmountAsync_ShouldUpdateAmount()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await _writeRepository.ChangeAmountAsync(
			recurringTransactionId: recurringTransaction.Id,
			amount: 10000m,
			expectedVersion: 0
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.Amount).IsEqualTo(expected: 10000m);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task ChangeCurrencyAsync_ShouldUpdateCurrency()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "USD");

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await _writeRepository.ChangeCurrencyAsync(
			recurringTransactionId: recurringTransaction.Id,
			currency: Core.ValueObjects.Currency.Create(value: "USD").Value,
			expectedVersion: 0
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.Currency.Value).IsEqualTo(expected: "USD");
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task ChangeDayOfMonthAsync_ShouldUpdateDayOfMonth()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await _writeRepository.ChangeDayOfMonthAsync(
			recurringTransactionId: recurringTransaction.Id,
			dayOfMonth: 20,
			expectedVersion: 0
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.DayOfMonth).IsEqualTo(expected: 20);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task DeactivateAsync_ShouldSetIsActiveToFalse()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await _writeRepository.DeactivateAsync(
			recurringTransactionId: recurringTransaction.Id,
			expectedVersion: 0
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.IsActive).IsFalse();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task ActivateAsync_ShouldSetIsActiveToTrue()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await _writeRepository.DeactivateAsync(recurringTransactionId: recurringTransaction.Id, expectedVersion: 0);
		await _writeRepository.ActivateAsync(recurringTransactionId: recurringTransaction.Id, expectedVersion: 1);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.IsActive).IsTrue();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task MarkExecutedAsync_ShouldSetLastExecutedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		DateTimeOffset executedAt = DateTimeOffset.UtcNow;
		await _writeRepository.MarkExecutedAsync(
			recurringTransactionId: recurringTransaction.Id,
			executedAt: executedAt,
			expectedVersion: 0
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.LastExecutedAt).IsNotNull();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task MarkMissedAsync_ShouldSetLastMissedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction =
			await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		DateTimeOffset missedAt = DateTimeOffset.UtcNow;
		await _writeRepository.MarkMissedAsync(
			recurringTransactionId: recurringTransaction.Id,
			missedAt: missedAt,
			expectedVersion: 0
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.LastMissedAt).IsNotNull();
		await Assert.That(value: entity.LastExecutedAt).IsNull();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task MarkMissedAsync_WithStaleVersion_ShouldThrowConcurrencyConflictException()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction =
			await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await Task.Run(function: async () => await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await _writeRepository.MarkMissedAsync(
			recurringTransactionId: recurringTransaction.Id,
			missedAt: DateTimeOffset.UtcNow,
			expectedVersion: 99
		)));
	}

	[Test]
	public async Task DeactivateByCategoryIdAsync_ShouldDeactivateAllTransactionsWithCategory()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, amount: 5000m, dayOfMonth: 15);
		await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, amount: 3000m, dayOfMonth: 20);

		await _writeRepository.DeactivateByCategoryIdAsync(categoryId: categoryId);

		List<RecurringTransactionEntity> entities = await Context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.CategoryId == categoryId)
			.ToListAsync();

		await Assert.That(value: entities.All(predicate: r => !r.IsActive)).IsTrue();
		await Assert.That(value: entities.All(predicate: r => r.RowVersion == 1)).IsTrue();
	}

	[Test]
	public async Task ChangeAmountAsync_WhenVersionConflict_ShouldThrowConcurrencyConflictException()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction =
			await CreateRecurringTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await _writeRepository.ChangeAmountAsync(
			recurringTransactionId: recurringTransaction.Id,
			amount: 10000m,
			expectedVersion: 0
		);

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await _writeRepository.ChangeAmountAsync(
			recurringTransactionId: recurringTransaction.Id,
			amount: 20000m,
			expectedVersion: 0
		));
	}
}
