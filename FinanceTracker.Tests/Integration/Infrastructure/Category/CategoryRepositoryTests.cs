using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Infrastructure.Database.Repositories;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;

namespace FinanceTracker.Tests.Integration.Infrastructure.Category;

public sealed class CategoryRepositoryTests : DatabaseFixture
{
	private CategoryRepository _categoryRepository = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
		=> _categoryRepository = new CategoryRepository(context: Context);

	private static Core.Domains.Category.Category CreateCategory(Guid? parentId = null)
	{
		return Core.Domains.Category.Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: parentId
		);
	}

	private async Task<Core.Domains.Category.Category> CreateAndSaveCategoryAsync(
		Guid userId,
		CategoryType type = CategoryType.Expense,
		bool isArchived = false,
		Guid? parentId = null)
	{
		Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
			userId: userId,
			name: "Еда",
			type: type,
			parentId: parentId
		);

		await _categoryRepository.CreateAsync(category: category);

		if (isArchived)
			await _categoryRepository.ArchiveAsync(categoryId: category.Id);

		return category;
	}
	
	[Test]
	public async Task GetByIdAsync_WithNonExistentCategory_ShouldReturnNull()
	{
		Core.Domains.Category.Category? result = await _categoryRepository.GetByIdAsync(categoryId: Guid.NewGuid());
		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task CreateAsync_ThenGetByIdAsync_ShouldReturnCorrectCategory()
	{
		Core.Domains.Category.Category category = CreateCategory();
		await _categoryRepository.CreateAsync(category: category);

		Core.Domains.Category.Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded.Id).IsEqualTo(expected: category.Id);
		await Assert.That(value: loaded.Name).IsEqualTo(expected: "Еда");
		await Assert.That(value: loaded.Type).IsEqualTo(expected: CategoryType.Expense);
		await Assert.That(value: loaded.IsArchived).IsFalse();
		await Assert.That(value: loaded.ParentId).IsNull();
	}

	[Test]
	public async Task CreateAsync_WithParentId_ShouldSetParentId()
	{
		Core.Domains.Category.Category parent = CreateCategory();
		await _categoryRepository.CreateAsync(category: parent);

		Core.Domains.Category.Category child = CreateCategory(parentId: parent.Id);
		await _categoryRepository.CreateAsync(category: child);

		Core.Domains.Category.Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: child.Id);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded.ParentId).IsEqualTo(expected: parent.Id);
	}

	[Test]
	public async Task RenameAsync_ShouldUpdateName()
	{
		Core.Domains.Category.Category category = CreateCategory();
		await _categoryRepository.CreateAsync(category: category);

		await _categoryRepository.RenameAsync(
			categoryId: category.Id,
			newName: "Продукты"
		);

		Core.Domains.Category.Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded.Name).IsEqualTo(expected: "Продукты");
	}

	[Test]
	public async Task ArchiveAsync_ShouldSetIsArchivedTrue()
	{
		Core.Domains.Category.Category category = CreateCategory();
		await _categoryRepository.CreateAsync(category: category);

		await _categoryRepository.ArchiveAsync(categoryId: category.Id);

		Core.Domains.Category.Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded.IsArchived).IsTrue();
	}

	[Test]
	public async Task UnarchiveAsync_ShouldSetIsArchivedFalse()
	{
		Core.Domains.Category.Category category = CreateCategory();
		await _categoryRepository.CreateAsync(category: category);

		await _categoryRepository.ArchiveAsync(categoryId: category.Id);
		await _categoryRepository.UnarchiveAsync(categoryId: category.Id);

		Core.Domains.Category.Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded.IsArchived).IsFalse();
	}
	
	[Test]
    public async Task GetAllAsync_WithNoCategories_ShouldReturnEmptyList()
    {
        IReadOnlyList<Core.Domains.Category.Category> result = await _categoryRepository.GetAllAsync(userId: Guid.NewGuid());

        await Assert.That(value: result.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnOnlyUserCategories()
    {
        Guid userId = Guid.NewGuid();
        _ = await CreateAndSaveCategoryAsync(userId: userId);
        _ = await CreateAndSaveCategoryAsync(userId: Guid.NewGuid());

        IReadOnlyList<Core.Domains.Category.Category> result = await _categoryRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].UserId).IsEqualTo(expected: userId);
    }

    [Test]
    public async Task GetAllAsync_WithTypeFilter_ShouldReturnOnlyMatchingCategories()
    {
        Guid userId = Guid.NewGuid();
        _ = await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Expense);
        _ = await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Income);

        IReadOnlyList<Core.Domains.Category.Category> result = await _categoryRepository.GetAllAsync(userId: userId, type: CategoryType.Expense);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].Type).IsEqualTo(expected: CategoryType.Expense);
    }

    [Test]
    public async Task GetAllAsync_WithIsArchivedFilter_ShouldReturnOnlyMatchingCategories()
    {
        Guid userId = Guid.NewGuid();
        _ = await CreateAndSaveCategoryAsync(userId: userId, isArchived: false);
        _ = await CreateAndSaveCategoryAsync(userId: userId, isArchived: true);

        IReadOnlyList<Core.Domains.Category.Category> result = await _categoryRepository.GetAllAsync(userId: userId, isArchived: false);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsArchived).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithParentIdFilter_ShouldReturnOnlySubcategories()
    {
        Guid userId = Guid.NewGuid();
        Core.Domains.Category.Category parent = await CreateAndSaveCategoryAsync(userId: userId);
        _ = await CreateAndSaveCategoryAsync(userId: userId, parentId: parent.Id);
        _ = await CreateAndSaveCategoryAsync(userId: userId);
		
        IReadOnlyList<Core.Domains.Category.Category> result = await _categoryRepository.GetAllAsync(userId: userId, parentId: parent.Id);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ParentId).IsEqualTo(parent.Id);
    }

    [Test]
    public async Task GetAllAsync_WithNullFilters_ShouldReturnAllCategories()
    {
        Guid userId = Guid.NewGuid();
        _ = await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Expense);
        _ = await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Income);
        _ = await CreateAndSaveCategoryAsync(userId: userId, isArchived: true);

        IReadOnlyList<Core.Domains.Category.Category> result = await _categoryRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 3);
    }
}