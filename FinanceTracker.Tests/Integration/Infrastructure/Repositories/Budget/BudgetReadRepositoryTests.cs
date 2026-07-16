using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Budget;

public sealed class BudgetReadRepositoryTests : DatabaseFixture
{
	private BudgetReadRepository _readRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private BudgetBuilder _budgetBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new BudgetReadRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_budgetBuilder = new BudgetBuilder(context: Context);
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
	}

	[Test]
	public async Task GetByIdAsync_WhenExists_ShouldReturnBudgetDto()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

		BudgetReadModel? result = await _readRepository.GetByIdAsync(budgetId: budgetId, userId: userId);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: budgetId);
		await Assert.That(value: result.Amount.Amount).IsEqualTo(expected: 10000m);
	}

	[Test]
	public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
	{
		BudgetReadModel? result = await _readRepository.GetByIdAsync(
			budgetId: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7()
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_ShouldNotReturnOtherUserBudget()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

		BudgetReadModel? result = await _readRepository.GetByIdAsync(
			budgetId: budgetId,
			userId: Guid.CreateVersion7()
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WhenDeactivated_ShouldReturnBudget()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

		BudgetWriteRepository writeRepository = new BudgetWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		await writeRepository.DeactivateAsync(budgetId: budgetId, expectedVersion: 0);

		BudgetReadModel? result = await _readRepository.GetByIdAsync(budgetId: budgetId, userId: userId);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.IsActive).IsFalse();
	}

	[Test]
	public async Task GetActiveByCategoryAsync_WhenDateInPeriod_ShouldReturnBudget()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

		BudgetReadModel? result = await _readRepository.GetActiveByCategoryAsync(
			userId: userId,
			categoryId: categoryId,
			date: new DateOnly(year: 2025, month: 1, day: 15)
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: budgetId);
	}

	[Test]
	public async Task GetActiveByCategoryAsync_WhenDateOutOfPeriod_ShouldReturnNull()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

		BudgetReadModel? result = await _readRepository.GetActiveByCategoryAsync(
			userId: userId,
			categoryId: categoryId,
			date: new DateOnly(year: 2025, month: 2, day: 1)
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnAllUserBudgets()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId1 = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid categoryId2 = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId1);
		await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId2);

		PagedResult<BudgetReadModel> result = await _readRepository.GetAllAsync(userId: userId);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_ShouldNotReturnOtherUserBudgets()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid anotherUserId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: anotherUserId);
		await _budgetBuilder.CreateAsync(userId: anotherUserId, categoryId: categoryId);

		PagedResult<BudgetReadModel> result = await _readRepository.GetAllAsync(userId: userId);

		await Assert.That(value: result.Items).IsEmpty();
		await Assert.That(value: result.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithoutCursor_WhenMoreItemsExist_ShouldSetHasNextPage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		for (int i = 0; i < 4; i++)
		{
			Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId, name: $"Категория {i}");
			await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		}

		PagedResult<BudgetReadModel> result = await _readRepository.GetAllAsync(userId: userId, pageSize: 3);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 3);
		await Assert.That(value: result.HasNextPage).IsTrue();
		await Assert.That(value: result.NextCursorDate).IsNotNull();
		await Assert.That(value: result.NextCursorId).IsNotNull();
	}

	[Test]
	public async Task GetAllAsync_WithCursor_ShouldReturnNextPage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		for (int i = 0; i < 4; i++)
		{
			Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId, name: $"Категория {i}");
			await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		}

		PagedResult<BudgetReadModel> firstPage = await _readRepository.GetAllAsync(userId: userId, pageSize: 3);
		BudgetReadModel lastItem = firstPage.Items[^1];

		PagedResult<BudgetReadModel> secondPage = await _readRepository.GetAllAsync(
			userId: userId,
			cursorCreatedAt: lastItem.CreatedAt,
			cursorId: lastItem.Id,
			pageSize: 3
		);

		await Assert.That(value: secondPage.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: secondPage.HasNextPage).IsFalse();
		await Assert.That(value: secondPage.Items.Any(b => firstPage.Items.Any(f => f.Id == b.Id))).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithIsActiveTrue_ShouldNotReturnDeactivatedBudgets()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId1 = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid categoryId2 = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId1);
		Guid deactivatedId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId2);

		BudgetWriteRepository writeRepository = new BudgetWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		await writeRepository.DeactivateAsync(budgetId: deactivatedId, expectedVersion: 0);

		PagedResult<BudgetReadModel> result = await _readRepository.GetAllAsync(userId: userId, isActive: true);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items.Any(b => b.Id == deactivatedId)).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithIsActiveFalse_ShouldReturnDeactivatedBudgets()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId1 = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid categoryId2 = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		Guid activatedId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId1);
		Guid deactivatedId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId2);

		BudgetWriteRepository writeRepository = new BudgetWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		await writeRepository.DeactivateAsync(budgetId: deactivatedId, expectedVersion: 0);

		PagedResult<BudgetReadModel> result = await _readRepository.GetAllAsync(userId: userId, isActive: false);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items.Any(b => b.Id == activatedId)).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithIsActiveNull_ShouldReturnAllBudgets()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId1 = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid categoryId2 = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		Guid activatedId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId1);
		Guid deactivatedId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId2);

		BudgetWriteRepository writeRepository = new BudgetWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		await writeRepository.DeactivateAsync(budgetId: deactivatedId, expectedVersion: 0);

		PagedResult<BudgetReadModel> result = await _readRepository.GetAllAsync(userId: userId);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.Items.Any(b => b.Id == activatedId)).IsTrue();
		await Assert.That(value: result.Items.Any(b => b.Id == deactivatedId)).IsTrue();
	}
}
