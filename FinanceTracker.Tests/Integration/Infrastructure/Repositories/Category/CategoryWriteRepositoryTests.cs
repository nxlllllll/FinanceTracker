using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Category;

public sealed class CategoryWriteRepositoryTests : DatabaseFixture
{
    private CategoryReadRepository _readRepository = null!;
    private CategoryWriteRepository _writeRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepository()
    {
        _readRepository = new CategoryReadRepository(context: Context);
        _writeRepository = new CategoryWriteRepository(context: Context);
    }

    private async Task<Core.Domains.Category.Category> CreateAndSaveCategoryAsync(Guid? parentId = null)
    {
        Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
            createdAt: FakeDateProvider.Default.UtcNow,
            userId: Guid.CreateVersion7(),
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
    }

    [Test]
    public async Task RenameAsync_ShouldUpdateName()
    {
        Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

        await _writeRepository.RenameAsync(categoryId: category.Id, newName: Name.Create(value: "Развлечения").Value);

        CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.Name.Value).IsEqualTo(expected: "Развлечения");
    }

    [Test]
    public async Task ArchiveAsync_ShouldSetIsArchivedTrue()
    {
        Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

        await _writeRepository.ArchiveAsync(categoryId: category.Id);

        CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.IsArchived).IsTrue();
    }

    [Test]
    public async Task UnarchiveAsync_ShouldSetIsArchivedFalse()
    {
        Core.Domains.Category.Category category = await CreateAndSaveCategoryAsync();

        await _writeRepository.ArchiveAsync(categoryId: category.Id);
        await _writeRepository.UnarchiveAsync(categoryId: category.Id);

        CategoryReadModel? loaded = await _readRepository.GetByIdAsync(categoryId: category.Id, userId: category.UserId);

        await Assert.That(value: loaded).IsNotNull();
        await Assert.That(value: loaded!.IsArchived).IsFalse();
    }
}