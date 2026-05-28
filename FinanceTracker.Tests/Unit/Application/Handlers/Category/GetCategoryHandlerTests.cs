using FinanceTracker.Application.UseCases.Category.Queries.GetCategory;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class GetCategoryHandlerTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private GetCategoryHandler _handler = null!;

	private static readonly Guid UserId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_handler = new GetCategoryHandler(categoryRepository: _categoryReadRepository);
	}

	[Test]
	public async Task Handle_WhenCategoryExists_ShouldReturnCategory()
	{
		CategoryReadModel category = CategoryFactory.CreateReadModel();

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		CategoryReadModel? result = await _handler.Handle(
			query: new GetCategoryQuery(CategoryId: category.Id, UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: category.Id);
	}

	[Test]
	public async Task Handle_WhenCategoryNotFound_ShouldReturnNull()
	{
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<CategoryReadModel?>(null));

		CategoryReadModel? result = await _handler.Handle(
			query: new GetCategoryQuery(CategoryId: Guid.CreateVersion7(), UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task Handle_ShouldPassBothCategoryIdAndUserIdToRepository()
	{
		Guid categoryId = Guid.CreateVersion7();

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<CategoryReadModel?>(null));

		await _handler.Handle(
			query: new GetCategoryQuery(CategoryId: categoryId, UserId: UserId),
			ct: CancellationToken.None
		);

		await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			categoryId: categoryId,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
