using FinanceTracker.Application.Categories.Queries.GetCategory;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class GetCategoryHandlerTests
{
	private ICategoryRepository _categoryRepository = null!;
	private GetCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_handler = new GetCategoryHandler(categoryRepository: _categoryRepository);
	}

	[Test]
	public async Task Handle_WhenCategoryExists_ShouldReturnCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = FinanceTracker.Core.Domains.Category.Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		GetCategoryQuery query = new GetCategoryQuery(CategoryId: category.Id);
		FinanceTracker.Core.Domains.Category.Category? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: category.Id);
	}

	[Test]
	public async Task Handle_WhenCategoryNotFound_ShouldReturnNull()
	{
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Category.Category?>(result: null));

		GetCategoryQuery query = new GetCategoryQuery(CategoryId: Guid.NewGuid());
		FinanceTracker.Core.Domains.Category.Category? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNull();
	}
}