using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Category;

public sealed class CategoryWriteRepositoryTests : DatabaseFixture
{
	private CategoryReadRepository _readRepository = null!;
	private CategoryWriteRepository _writeRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_readRepository = new CategoryReadRepository(context: Context);
		_writeRepository = new CategoryWriteRepository(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private async Task<Core.Domains.Category.Category> CreateAndSaveCategoryAsync(Guid? parentId = null)
	{
		Core.ValueObjects.Currency currency = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currency);

		Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId,
			name: Name.Create(value: "Еда").Value,
			type: CategoryType.Expense,
			parentId: parentId
		);

		await _writeRepository.CreateAsync(category: category);
		await Context.SaveChangesAsync();
		return category;
	}

	[Test]
	public async Task CreateAsync_ShouldPersistCategory()
	{
		Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

		CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.Id).IsEqualTo(expected: category.Id);
		await Assert.That(value: loaded.Name.Value).IsEqualTo(expected: "Еда");
		await Assert.That(value: loaded.IsArchived).IsFalse();

		CategoryEntity? entity = await Context.Categories.AsNoTracking().FirstOrDefaultAsync(predicate: c => c.Id == category.Id);
		await Assert.That(value: entity!.RowVersion).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task RenameAsync_ShouldUpdateName()
	{
		Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

		await _writeRepository.RenameAsync(
			categoryId: category.Id,
			newName: Name.Create(value: "Развлечения").Value,
			expectedVersion: 0
		);

		CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.Name.Value).IsEqualTo(expected: "Развлечения");

		CategoryEntity entity = await Context.Categories.AsNoTracking().FirstAsync(predicate: c => c.Id == category.Id);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task ArchiveAsync_ShouldSetIsArchivedTrue()
	{
		Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

		await _writeRepository.ArchiveAsync(categoryId: category.Id, expectedVersion: 0);

		CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.IsArchived).IsTrue();

		CategoryEntity entity = await Context.Categories.AsNoTracking().FirstAsync(predicate: c => c.Id == category.Id);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task UnarchiveAsync_ShouldSetIsArchivedFalse()
	{
		Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

		await _writeRepository.ArchiveAsync(categoryId: category.Id, expectedVersion: 0);
		await _writeRepository.UnarchiveAsync(categoryId: category.Id, expectedVersion: 1);

		CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.IsArchived).IsFalse();

		CategoryEntity entity = await Context.Categories.AsNoTracking().FirstAsync(predicate: c => c.Id == category.Id);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task ArchiveAsync_WhenVersionConflict_ShouldThrowConcurrencyConflictException()
	{
		Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

		await _writeRepository.ArchiveAsync(categoryId: category.Id, expectedVersion: 0);

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () =>
			await _writeRepository.ArchiveAsync(categoryId: category.Id, expectedVersion: 0)
		);
	}
}
