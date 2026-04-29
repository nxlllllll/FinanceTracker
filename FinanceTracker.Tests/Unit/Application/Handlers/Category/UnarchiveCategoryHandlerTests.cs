using FinanceTracker.Application.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class UnarchiveCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository = null!;
	private UnarchiveCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_handler = new UnarchiveCategoryHandler(categoryRepository: _categoryRepository);
	}
	
	[Test]
	public async Task Handle_WithArchivedCategory_ShouldUnarchiveCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true);
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(
			UserId: category.UserId,
			CategoryId: category.Id
		);
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
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Category.Category?>(result: null));

		UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(
			UserId: Guid.NewGuid(),
			CategoryId: Guid.NewGuid()
		);

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenCategoryNotArchived_ShouldThrowUnarchivingException()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();

		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		UnarchiveCategoryCommand command = new UnarchiveCategoryCommand(
			UserId: category.UserId,
			CategoryId: category.Id
		);

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<UnarchivingException>();
	}
}