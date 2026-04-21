using FinanceTracker.Application.Categories.Commands.CreateCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class CreateCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository;
	private CreateCategoryHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_handler = new CreateCategoryHandler(categoryRepository: _categoryRepository);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateCategory()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.NewGuid(),
			Name: "Еда",
			Type: CategoryType.Expense,
			ParentId: null
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<Core.Domains.Category.Category>(c =>
				c.Name == "Еда" &&
				c.Type == CategoryType.Expense &&
				c.IsArchived == false
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithParentId_ShouldCreateCategoryWithParentId()
	{
		Guid parentId = Guid.NewGuid();
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.NewGuid(),
			Name: "Фастфуд",
			Type: CategoryType.Expense,
			ParentId: parentId
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<Core.Domains.Category.Category>(c => c.ParentId == parentId),
			ct: Arg.Any<CancellationToken>()
		);
	}
}