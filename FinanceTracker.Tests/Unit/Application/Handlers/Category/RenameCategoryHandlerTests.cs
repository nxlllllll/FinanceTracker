using FinanceTracker.Application.Categories.Commands.RenameCategory;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Tests.Unit.Helpers;
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

	[Test]
	public async Task HandleAsync_ShouldCallRenameWithNewName()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();

		await _handler.HandleAsync(
			command: new RenameCategoryCommand(UserId: category.UserId, CategoryId: category.Id, NewName: "Транспорт"),
			category: category,
			ct: CancellationToken.None
		);

		await _categoryRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
			categoryId: category.Id,
			newName: "Транспорт",
			ct: Arg.Any<CancellationToken>()
		);
	}
}