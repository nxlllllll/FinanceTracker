using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Data;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Budget;

public sealed class BudgetWriteRepositoryTests : DatabaseFixture
{
	private BudgetWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new BudgetWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
	}

	private async Task<Core.Domains.Budget.Budget> CreateAndSaveBudgetAsync(
		Guid userId,
		Guid categoryId,
		int monthOffset = 0)
	{
		Result<Core.Domains.Budget.Budget, DomainException> result = Core.Domains.Budget.Budget.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			categoryId: categoryId,
			amount: Money.Create(amount: 10000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			from: new DateOnly(year: 2025, month: 1 + monthOffset, day: 1),
			to: new DateOnly(year: 2025, month: 1 + monthOffset, day: 28)
		);

		Core.Domains.Budget.Budget budget = result.Value!;
		await _writeRepository.CreateAsync(budget: budget);
		await Context.SaveChangesAsync();
		return budget;
	}

	[Test]
	public async Task CreateAsync_ShouldCreateBudgetAndProgress()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Budget.Budget budget = await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId);

		BudgetEntity? budgetEntity = await Context.Budgets.FirstOrDefaultAsync(predicate: b => b.Id == budget.Id);
		BudgetProgressEntity? progress = await Context.BudgetProgresses.FirstOrDefaultAsync(predicate: p => p.BudgetId == budget.Id);

		await Assert.That(value: budgetEntity).IsNotNull();
		await Assert.That(value: budgetEntity!.Amount).IsEqualTo(expected: 10000m);
		await Assert.That(value: budgetEntity.IsActive).IsTrue();
		await Assert.That(value: budgetEntity.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: budgetEntity.RowVersion).IsEqualTo(expected: 0);
		await Assert.That(value: progress).IsNotNull();
		await Assert.That(value: progress!.Spent).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task ChangePeriodAsync_ShouldUpdatePeriod()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Budget.Budget budget = await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId);

		await _writeRepository.ChangePeriodAsync(
			budgetId: budget.Id,
			from: new DateOnly(year: 2025, month: 2, day: 1),
			to: new DateOnly(year: 2025, month: 2, day: 28),
			expectedVersion: 0
		);

		BudgetEntity result = await Context.Budgets.AsNoTracking().FirstAsync(predicate: b => b.Id == budget.Id);

		await Assert.That(value: result.From).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 1));
		await Assert.That(value: result.To).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 28));
		await Assert.That(value: result.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task DeactivateAsync_ShouldSetIsActiveFalse()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Budget.Budget budget = await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId);

		await _writeRepository.DeactivateAsync(budgetId: budget.Id, expectedVersion: 0);

		BudgetEntity? result = await Context.Budgets.AsNoTracking().FirstOrDefaultAsync(predicate: b => b.Id == budget.Id);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.IsActive).IsFalse();
		await Assert.That(value: result.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task DeactivateByCategoryIdAsync_ShouldDeactivateAllBudgetsForCategory()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		for (int i = 0; i < 3; i++)
			await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId, monthOffset: i);

		await _writeRepository.DeactivateByCategoryIdAsync(categoryId: categoryId);

		List<BudgetEntity> results = await Context.Budgets.AsNoTracking()
			.Where(predicate: b => b.CategoryId == categoryId)
			.ToListAsync();

		await Assert.That(value: results.Count).IsEqualTo(expected: 3);
		await Assert.That(value: results.All(b => !b.IsActive)).IsTrue();
		await Assert.That(value: results.All(b => b.RowVersion == 1)).IsTrue();
	}

	[Test]
	public async Task ActivateAsync_ShouldSetIsActiveTrue()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Budget.Budget budget = await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId);

		await _writeRepository.DeactivateAsync(budgetId: budget.Id, expectedVersion: 0);
		await _writeRepository.ActivateAsync(budgetId: budget.Id, expectedVersion: 1);

		BudgetEntity? result = await Context.Budgets.AsNoTracking().FirstOrDefaultAsync(predicate: b => b.Id == budget.Id);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.IsActive).IsTrue();
		await Assert.That(value: result.RowVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task ChangeAmountAsync_WhenVersionConflict_ShouldThrowConcurrencyConflictException()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Budget.Budget budget = await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId);

		await _writeRepository.ChangeAmountAsync(budgetId: budget.Id, amount: 5000m, expectedVersion: 0);

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () =>
			await _writeRepository.ChangeAmountAsync(budgetId: budget.Id, amount: 9000m, expectedVersion: 0)
		);
	}

	[Test]
	public async Task CreateAsync_WhenPeriodOverlapsExistingBudgetForSameCategory_ShouldThrowUniqueConstraintException()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await CreateAndSaveBudgetAsync(userId: userId, categoryId: categoryId);

		Result<Core.Domains.Budget.Budget, DomainException> overlappingResult = Core.Domains.Budget.Budget.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			from: new DateOnly(year: 2025, month: 1, day: 15),
			to: new DateOnly(year: 2025, month: 2, day: 15)
		);
		Core.Domains.Budget.Budget overlappingBudget = overlappingResult.Value!;

		EFUnitOfWork unitOfWork = new EFUnitOfWork(context: Context, logger: NullLogger<EFUnitOfWork>.Instance);

		await Assert.ThrowsAsync<UniqueConstraintException>(action: async () => await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			await _writeRepository.CreateAsync(budget: overlappingBudget, ct: CancellationToken.None)
		));
	}

	[Test]
	public async Task CreateAsync_WhenPeriodIsSingleDay_ShouldThrowCheckConstraintException()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateOnly sameDay = new DateOnly(year: 2025, month: 3, day: 10);
		Core.Domains.Budget.Budget singleDayBudget = Core.Domains.Budget.Budget.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: userId,
			categoryId: categoryId,
			amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			isActive: true,
			from: sameDay,
			to: sameDay,
			rowVersion: 0,
			createdAt: FakeDateProvider.Default.UtcNow
		);

		EFUnitOfWork unitOfWork = new EFUnitOfWork(context: Context, logger: NullLogger<EFUnitOfWork>.Instance);

		await Assert.ThrowsAsync<CheckConstraintException>(action: async () => await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			await _writeRepository.CreateAsync(budget: singleDayBudget, ct: CancellationToken.None)
		));
	}
}
