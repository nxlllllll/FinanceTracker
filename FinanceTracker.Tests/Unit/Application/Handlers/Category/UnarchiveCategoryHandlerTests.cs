using FinanceTracker.Application.Categories.Commands.UnarchiveCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class UnarchiveCategoryHandlerTests
{
	private ICategoryWriteRepository _categoryWriteRepository = null!;
	private UnarchiveCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryWriteRepository = Substitute.For<ICategoryWriteRepository>();
		_handler = new UnarchiveCategoryHandler(categoryWriteRepository: _categoryWriteRepository);
	}

	[Test]
	public async Task HandleAsync_WithArchivedCategory_ShouldCallUnarchive()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create(archived: true);

		await _handler.HandleAsync(
			command: new UnarchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		);

		await _categoryWriteRepository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
			categoryId: category.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCategoryAlreadyActive_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new UnarchiveCategoryCommand(UserId: category.UserId, CategoryId: category.Id),
			category: category,
			ct: CancellationToken.None
		)).Throws<UnarchivingException>();
	}
}