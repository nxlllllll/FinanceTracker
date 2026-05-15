using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Category;

public sealed class CategoryReadRepositoryTests : DatabaseFixture
{
    private CategoryReadRepository _readRepository = null!;
    private CategoryWriteRepository _writeRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
    {
        _readRepository = new CategoryReadRepository(context: Context);
        _writeRepository = new CategoryWriteRepository(context: Context);
    }

    private async Task<Core.Domains.Category.Category> CreateAndSaveCategoryAsync(
        Guid userId,
        CategoryType type = CategoryType.Expense,
        bool isArchived = false,
        Guid? parentId = null)
    {
        Result<Core.Domains.Category.Category, DomainException> result = Core.Domains.Category.Category.Create(
            createdAt: FakeDateProvider.Default.UtcNow,
            userId: userId,
            name: Name.Create(value: "Еда").Value,
            type: type,
            parentId: parentId
        );
        Core.Domains.Category.Category category = result.Value!;
        
        await _writeRepository.CreateAsync(category: category);
        if (isArchived)
            await _writeRepository.ArchiveAsync(categoryId: category.Id);
        return category;
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentCategory_ShouldReturnNull()
    {
        Core.Domains.Category.Category? result = await _readRepository.GetByIdAsync(categoryId: Guid.CreateVersion7(), userId: Guid.CreateVersion7());
        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithExistingCategory_ShouldReturnCorrectCategory()
    {
        Guid userId = Guid.CreateVersion7();
        Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync(userId: userId);

        Core.Domains.Category.Category? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: userId);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.Id).IsEqualTo(expected: category.Id);
        await Assert.That(value: loaded.Name.Value).IsEqualTo(expected: "Еда");
        await Assert.That(value: loaded.Type).IsEqualTo(expected: CategoryType.Expense);
        await Assert.That(value: loaded.IsArchived).IsFalse();
        await Assert.That(value: loaded.ParentId).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithParentId_ShouldSetParentId()
    {
        Guid userId = Guid.CreateVersion7();
        Core.Domains.Category.Category parent = await CreateAndSaveCategoryAsync(userId: userId);
        Core.Domains.Category.Category child = await CreateAndSaveCategoryAsync(userId: userId, parentId: parent.Id);

        Core.Domains.Category.Category? loaded = await _readRepository.GetByIdAsync(categoryId: child.Id, userId: userId);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.ParentId).IsEqualTo(expected: parent.Id);
    }

    [Test]
    public async Task GetAllAsync_WithNoCategories_ShouldReturnEmptyList()
    {
        IReadOnlyList<Core.Domains.Category.Category> result = await _readRepository.GetAllAsync(userId: Guid.CreateVersion7());
        await Assert.That(value: result.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnOnlyUserCategories()
    {
        Guid userId = Guid.CreateVersion7();
        await CreateAndSaveCategoryAsync(userId: userId);
        await CreateAndSaveCategoryAsync(userId: Guid.CreateVersion7());

        IReadOnlyList<Core.Domains.Category.Category> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].UserId).IsEqualTo(expected: userId);
    }

    [Test]
    public async Task GetAllAsync_WithTypeFilter_ShouldReturnOnlyMatchingCategories()
    {
        Guid userId = Guid.CreateVersion7();
        await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Expense);
        await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Income);

        IReadOnlyList<Core.Domains.Category.Category> result = await _readRepository.GetAllAsync(userId: userId, type: CategoryType.Expense);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].Type).IsEqualTo(expected: CategoryType.Expense);
    }

    [Test]
    public async Task GetAllAsync_WithIsArchivedFilter_ShouldReturnOnlyMatchingCategories()
    {
        Guid userId = Guid.CreateVersion7();
        await CreateAndSaveCategoryAsync(userId: userId, isArchived: false);
        await CreateAndSaveCategoryAsync(userId: userId, isArchived: true);

        IReadOnlyList<Core.Domains.Category.Category> result = await _readRepository.GetAllAsync(userId: userId, isArchived: false);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsArchived).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithParentIdFilter_ShouldReturnOnlySubcategories()
    {
        Guid userId = Guid.CreateVersion7();
        Core.Domains.Category.Category parent = await CreateAndSaveCategoryAsync(userId: userId);
        await CreateAndSaveCategoryAsync(userId: userId, parentId: parent.Id);
        await CreateAndSaveCategoryAsync(userId: userId);

        IReadOnlyList<Core.Domains.Category.Category> result = await _readRepository.GetAllAsync(userId: userId, parentId: parent.Id);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].ParentId).IsEqualTo(expected: parent.Id);
    }

    [Test]
    public async Task GetAllAsync_WithNullFilters_ShouldReturnAllCategories()
    {
        Guid userId = Guid.CreateVersion7();
        await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Expense);
        await CreateAndSaveCategoryAsync(userId: userId, type: CategoryType.Income);
        await CreateAndSaveCategoryAsync(userId: userId, isArchived: true);

        IReadOnlyList<Core.Domains.Category.Category> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 3);
    }
}