using FinanceTracker.Application.UseCases.Category.Queries.GetCategory;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
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
		_handler = new GetCategoryHandler(categoryReadRepository: _categoryReadRepository);
	}

	[Test]
	public async Task Handle_WhenCategoryExists_ShouldReturnSuccess()
	{
		CategoryReadModel model = CategoryFactory.CreateReadModel();
		GetCategoryQuery query = new GetCategoryQuery(
			CategoryId: model.Id,
			UserId: model.UserId
		);

		_categoryReadRepository
			.GetByIdAsync(categoryId: model.Id, userId: model.UserId, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: model);

		Result<CategoryReadModel, DomainException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenCategoryNotFound_ShouldReturnNotFound()
	{
		Guid categoryId = Guid.CreateVersion7();
		GetCategoryQuery query = new GetCategoryQuery(
			CategoryId: categoryId,
			UserId: Guid.CreateVersion7()
		);

		_categoryReadRepository
			.GetByIdAsync(categoryId: categoryId, userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: (CategoryReadModel?)null);

		Result<CategoryReadModel, DomainException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}