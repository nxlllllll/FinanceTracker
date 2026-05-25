using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
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
		_writeRepository = new RecurringTransactionWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
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

		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: "Monthly rent"
		
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		RecurringTransactionEntity? entity = await Context.RecurringTransactions
			.FirstOrDefaultAsync(predicate: r => r.Id == recurringTransaction.Id);

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

		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		await _writeRepository.ChangeAmountAsync(recurringTransactionId: recurringTransaction.Id, amount: 10000m);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.Amount).IsEqualTo(expected: 10000m);
	}

	[Test]
	public async Task ChangeCurrencyAsync_ShouldUpdateCurrency()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await new CurrencyBuilder(context: Context).CreateAsync(code: "USD");
		
		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		await _writeRepository.ChangeCurrencyAsync(
			recurringTransactionId: recurringTransaction.Id, 
			currency: Core.ValueObjects.Currency.Create(value: "USD").Value
		);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.Currency.Value).IsEqualTo(expected: "USD");
	}

	[Test]
	public async Task ChangeDayOfMonthAsync_ShouldUpdateDayOfMonth()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		
		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		await _writeRepository.ChangeDayOfMonthAsync(recurringTransactionId: recurringTransaction.Id, dayOfMonth: 20);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.DayOfMonth).IsEqualTo(expected: 20);
	}

	[Test]
	public async Task DeactivateAsync_ShouldSetIsActiveToFalse()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		await _writeRepository.DeactivateAsync(recurringTransactionId: recurringTransaction.Id);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.IsActive).IsFalse();
	}

	[Test]
	public async Task ActivateAsync_ShouldSetIsActiveToTrue()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		await _writeRepository.DeactivateAsync(recurringTransactionId: recurringTransaction.Id);
		await _writeRepository.ActivateAsync(recurringTransactionId: recurringTransaction.Id);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.IsActive).IsTrue();
	}

	[Test]
	public async Task MarkExecutedAsync_ShouldSetLastExecutedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> result = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			dayOfMonth: 15,
			description: null
		);
		Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = result.Value!;
		
		await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);

		DateTimeOffset executedAt = DateTimeOffset.UtcNow;
		await _writeRepository.MarkExecutedAsync(recurringTransactionId: recurringTransaction.Id, executedAt: executedAt);

		RecurringTransactionEntity entity = await Context.RecurringTransactions.AsNoTracking()
			.FirstAsync(predicate: r => r.Id == recurringTransaction.Id);

		await Assert.That(value: entity.LastExecutedAt).IsNotNull();
	}

	[Test]
public async Task DeactivateByCategoryIdAsync_ShouldDeactivateAllTransactionsWithCategory()
{
    Guid userId = await _userBuilder.CreateAsync();
    Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
    Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

    Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> firstResult = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
		createdAt: FakeDateProvider.Default.UtcNow,
		userId: userId,
		accountId: accountId,
		categoryId: categoryId,
		amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
		direction: DirectionType.Debit,
		dayOfMonth: 15,
		description: null
	);
	Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction = firstResult.Value!;

	Result<Core.Domains.RecurringTransaction.RecurringTransaction, DomainException> secondResult = Core.Domains.RecurringTransaction.RecurringTransaction.Create(
		createdAt: FakeDateProvider.Default.UtcNow,
		userId: userId,
		accountId: accountId,
		categoryId: categoryId,
		amount: Money.Create(amount: 3000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
		direction: DirectionType.Debit,
		dayOfMonth: 20,
		description: null
	);
	Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction2 = secondResult.Value!;
	
    await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction);
    await _writeRepository.CreateAsync(recurringTransaction: recurringTransaction2); 

    await _writeRepository.DeactivateByCategoryIdAsync(categoryId: categoryId);

    List<RecurringTransactionEntity> entities = await Context.RecurringTransactions.AsNoTracking()
        .Where(predicate: r => r.CategoryId == categoryId)
        .ToListAsync();

    await Assert.That(value: entities.All(predicate: r => !r.IsActive)).IsTrue();
}
}
