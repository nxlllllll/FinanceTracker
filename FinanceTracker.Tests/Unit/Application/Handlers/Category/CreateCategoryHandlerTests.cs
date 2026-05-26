using FinanceTracker.Application.UseCases.Category.Commands.CreateCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class CreateCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private CreateCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_handler = new CreateCategoryHandler(categoryWriteRepository: _categoryWriteRepository, dateProvider: FakeDateProvider.Default);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateCategory()
	{
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "���").Value,
			Type: CategoryType.Expense,
			ParentId: null
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<FinanceTracker.Core.Domains.Category.Category>(c =>
				c.Name == "���" &&
				c.Type == CategoryType.Expense &&
				c.IsArchived == false
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithParentId_ShouldCreateCategoryWithParentId()
	{
		Guid parentId = Guid.CreateVersion7();
		CreateCategoryCommand command = new CreateCategoryCommand(
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "�������").Value,
			Type: CategoryType.Expense,
			ParentId: parentId
		);

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			category: Arg.Is<FinanceTracker.Core.Domains.Category.Category>(c => c.ParentId == parentId),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
