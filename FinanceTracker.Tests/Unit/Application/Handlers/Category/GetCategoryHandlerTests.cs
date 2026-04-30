using FinanceTracker.Application.Categories.Queries.GetCategory;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class GetCategoryHandlerTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private GetCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_handler = new GetCategoryHandler(categoryRepository: _categoryReadRepository);
	}

	[Test]
	public async Task Handle_WhenCategoryExists_ShouldReturnCategory()
	{
		FinanceTracker.Core.Domains.Category.Category category = CategoryFactory.Create();
		
		_categoryReadRepository.GetByIdAsync(
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
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Category.Category?>(result: null));

		GetCategoryQuery query = new GetCategoryQuery(CategoryId: Guid.NewGuid());
		FinanceTracker.Core.Domains.Category.Category? result = await _handler.Handle(query: query, ct: CancellationToken.None);

		await Assert.That(value: result).IsNull();
	}
}