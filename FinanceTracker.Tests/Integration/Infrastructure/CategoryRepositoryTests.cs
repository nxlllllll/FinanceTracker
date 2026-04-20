using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Infrastructure.Database.Repositories;

namespace FinanceTracker.Tests.Integration.Infrastructure;

public sealed class CategoryRepositoryTests : DatabaseFixture
{
	private CategoryRepository _categoryRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
        => _categoryRepository = new CategoryRepository(context: Context);

    private static Category CreateCategory(Guid? parentId = null)
    {
        return Category.Create(
            userId: Guid.NewGuid(),
            name: "Еда",
            type: CategoryType.Expense,
            parentId: parentId
        );
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentCategory_ShouldReturnNull()
    {
        Category? result = await _categoryRepository.GetByIdAsync(categoryId: Guid.NewGuid());
        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task CreateAsync_ThenGetByIdAsync_ShouldReturnCorrectCategory()
    {
        Category category = CreateCategory();
        await _categoryRepository.CreateAsync(category: category);

        Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

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
        Category parent = CreateCategory();
        await _categoryRepository.CreateAsync(category: parent);

        Category child = CreateCategory(parentId: parent.Id);
        await _categoryRepository.CreateAsync(category: child);

        Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: child.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.ParentId).IsEqualTo(expected: parent.Id);
    }

    [Test]
    public async Task RenameAsync_ShouldUpdateName()
    {
        Category category = CreateCategory();
        await _categoryRepository.CreateAsync(category: category);

        await _categoryRepository.RenameAsync(
            categoryId: category.Id,
            newName: "Продукты"
        );

        Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.Name).IsEqualTo(expected: "Продукты");
    }

    [Test]
    public async Task ArchiveAsync_ShouldSetIsArchivedTrue()
    {
        Category category = CreateCategory();
        await _categoryRepository.CreateAsync(category: category);

        await _categoryRepository.ArchiveAsync(categoryId: category.Id);

        Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.IsArchived).IsTrue();
    }

    [Test]
    public async Task UnarchiveAsync_ShouldSetIsArchivedFalse()
    {
        Category category = CreateCategory();
        await _categoryRepository.CreateAsync(category: category);

        await _categoryRepository.ArchiveAsync(categoryId: category.Id);
        await _categoryRepository.UnarchiveAsync(categoryId: category.Id);

        Category? loaded = await _categoryRepository.GetByIdAsync(categoryId: category.Id);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded.IsArchived).IsFalse();
    }
}