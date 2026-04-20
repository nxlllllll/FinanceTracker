using FinanceTracker.Application.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class UnarchiveCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository;
    private UnarchiveCategoryHandler _handler;

    [Before(hookType: Test)]
    public void Setup()
    {
        _categoryRepository = Substitute.For<ICategoryRepository>();
        _handler = new UnarchiveCategoryHandler(categoryRepository: _categoryRepository);
    }

    private static Core.Domains.Category.Category CreateArchivedCategory()
    {
        Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
            userId: Guid.NewGuid(),
            name: "Еда",
            type: CategoryType.Expense,
            parentId: null
        );
        category.Archive();
        return category;
    }

    [Test]
    public async Task Handle_WithArchivedCategory_ShouldUnarchiveCategory()
    {
        Core.Domains.Category.Category category = CreateArchivedCategory();
        _categoryRepository.GetByIdAsync(
            categoryId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: category);

        UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(CategoryId: category.Id);
        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _categoryRepository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
            categoryId: category.Id,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenCategoryNotFound_ShouldThrowNotFoundException()
    {
        _categoryRepository.GetByIdAsync(
            categoryId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<Core.Domains.Category.Category?>(result: null));

        UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(CategoryId: Guid.NewGuid());

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenCategoryNotArchived_ShouldThrowUnarchivingException()
    {
        Core.Domains.Category.Category category = Core.Domains.Category.Category.Create(
            userId: Guid.NewGuid(),
            name: "Еда",
            type: CategoryType.Expense,
            parentId: null
        );

        _categoryRepository.GetByIdAsync(
            categoryId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: category);

        UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(CategoryId: category.Id);

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<UnarchivingException>();
    }
}