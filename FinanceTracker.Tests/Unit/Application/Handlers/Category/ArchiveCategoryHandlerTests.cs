using FinanceTracker.Application.Categories.Commands.ArchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class ArchiveCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository = null!;
	private ArchiveCategoryHandler _handler = null!;

	[Before(Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_handler = new ArchiveCategoryHandler(categoryRepository: _categoryRepository);
	}

	private static FinanceTracker.Core.Domains.Category.Category CreateCategory() => FinanceTracker.Core.Domains.Category.Category.Create(
		userId: Guid.NewGuid(),
		name: "Еда",
		type: CategoryType.Expense,
		parentId: null
	);

	[Test]
	public async Task Handle_WithActiveCategory_ShouldArchiveCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = CreateCategory();
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		ArchiveCategoryCommand command = new ArchiveCategoryCommand(CategoryId: category.Id);
		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
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
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Category.Category?>(result: null));

		ArchiveCategoryCommand command = new ArchiveCategoryCommand(CategoryId: Guid.NewGuid());

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenCategoryAlreadyArchived_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Category.Category category = CreateCategory();
		category.Archive();

		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		ArchiveCategoryCommand command = new ArchiveCategoryCommand(CategoryId: category.Id);

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<ArchivingException>();
	}
}