using FinanceTracker.Application.Categories.Commands.RenameCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class RenameCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository = null!;
	private RenameCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_handler = new RenameCategoryHandler(categoryRepository: _categoryRepository);
	}

	private static FinanceTracker.Core.Domains.Category.Category CreateCategory()
	{
		return FinanceTracker.Core.Domains.Category.Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldRenameCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = CreateCategory();
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		RenameCategoryCommand command = new RenameCategoryCommand(
			CategoryId: category.Id,
			NewName: "Продукты"
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
			categoryId: category.Id,
			newName: "Продукты",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenCategoryNotFound_ShouldThrowNotFoundException()
	{
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Category.Category?>(result: null));

		RenameCategoryCommand command = new RenameCategoryCommand(
			CategoryId: Guid.NewGuid(),
			NewName: "Продукты"
		);

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenCategoryArchived_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Category.Category category = CreateCategory();
		category.Archive();

		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		RenameCategoryCommand command = new RenameCategoryCommand(
			CategoryId: category.Id,
			NewName: "Продукты"
		);

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<ArchivingException>();
	}
}