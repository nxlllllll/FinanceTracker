using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.RecurringTransaction;

public sealed class RecurringTransactionWriteRepositoryTests : DatabaseFixture
{
	private RecurringTransactionWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new RecurringTransactionWriteRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
	}

	[Test]
	public async Task CreateAsync_ShouldCreateRecurringTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: "Monthly rent"
		);

		RecurringTransactionEntity? entity = await Context.RecurringTransactions
			.FirstOrDefaultAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.Amount).IsEqualTo(expected: 5000m);
		await Assert.That(value: entity.DayOfMonth).IsEqualTo(expected: 15);
		await Assert.That(value: entity.IsActive).IsTrue();
		await Assert.That(value: entity.LastExecutedAt).IsNull();
	}

	[Test]
	public async Task ChangeAmountAsync_ShouldUpdateAmount()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		await _writeRepository.ChangeAmountAsync(recurringTransactionId: id, amount: 10000m);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity.Amount).IsEqualTo(expected: 10000m);
	}

	[Test]
	public async Task ChangeCurrencyAsync_ShouldUpdateCurrency()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await new CurrencyBuilder(context: Context).CreateAsync(code: "USD");
		
		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		await _writeRepository.ChangeCurrencyAsync(recurringTransactionId: id, currency: "USD");

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity.Currency).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task ChangeDayOfMonthAsync_ShouldUpdateDayOfMonth()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		await _writeRepository.ChangeDayOfMonthAsync(recurringTransactionId: id, dayOfMonth: 20);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity.DayOfMonth).IsEqualTo(expected: 20);
	}

	[Test]
	public async Task DeactivateAsync_ShouldSetIsActiveToFalse()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity.IsActive).IsFalse();
	}

	[Test]
	public async Task ActivateAsync_ShouldSetIsActiveToTrue()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id);
		await _writeRepository.ActivateAsync(recurringTransactionId: id);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity.IsActive).IsTrue();
	}

	[Test]
	public async Task MarkExecutedAsync_ShouldSetLastExecutedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		DateTime executedAt = DateTime.UtcNow;
		await _writeRepository.MarkExecutedAsync(recurringTransactionId: id, executedAt: executedAt);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == id);

		await Assert.That(value: entity.LastExecutedAt).IsNotNull();
	}

	[Test]
	public async Task DeactivateByCategoryIdAsync_ShouldDeactivateAllTransactionsWithCategory()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id1 = Guid.NewGuid();
		Guid id2 = Guid.NewGuid();

		await _writeRepository.CreateAsync(
			recurringTransactionId: id1,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 5000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);

		await _writeRepository.CreateAsync(
			recurringTransactionId: id2,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3000m,
			currency: "RUB",
			direction: DirectionType.Debit,
			dayOfMonth: 20,
			description: null
		);

		await _writeRepository.DeactivateByCategoryIdAsync(categoryId: categoryId);

		List<RecurringTransactionEntity> entities = await Context.RecurringTransactions.AsNoTracking()
			.Where(predicate: r => r.CategoryId == categoryId)
			.ToListAsync();

		await Assert.That(value: entities.All(predicate: r => !r.IsActive)).IsTrue();
	}
}