using FinanceTracker.Application.UseCases.Category.Commands.RenameCategory;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class RenameCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private RenameCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_handler = new RenameCategoryHandler(categoryWriteRepository: _categoryWriteRepository);
	}

	[Test]
	public async Task HandleAsync_ShouldCallRenameWithNewName()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create().Value!;
		Name name = Name.Create(value: "Транспорт").Value;

		await _handler.HandleAsync(
			command: new RenameCategoryCommand(UserId: category.UserId, CategoryId: category.Id, NewName: name),
			category: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
			categoryId: category.Id,
			newName: name,
			ct: Arg.Any<CancellationToken>()
		);
	}
}