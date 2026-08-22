using FinanceTracker.Application.UseCases.Category.Queries.GetCategories;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class GetCategoriesHandlerTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private GetCategoriesHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_handler = new GetCategoriesHandler(categoryReadRepository: _categoryReadRepository);
	}

	private static PagedResult<CategoryReadModel> EmptyPage()
	{
		return new PagedResult<CategoryReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<CategoryReadModel> PageOf(IReadOnlyList<CategoryReadModel> items)
	{
		return new PagedResult<CategoryReadModel>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllCategories()
	{
		IReadOnlyList<CategoryReadModel> categories = [
			CategoryFactory.CreateReadModel(),
			CategoryFactory.CreateReadModel()
		];

		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: categories));

		Result<PagedResult<CategoryReadModel>, AppException> result = await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_WhenNoCategories_ShouldReturnEmptyList()
	{
		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		Result<PagedResult<CategoryReadModel>, AppException> result = await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Items).IsEmpty();
	}

	[Test]
	public async Task Handle_ShouldPassTypeFilterToRepository()
	{
		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7(), Type: CategoryType.Income),
			ct: CancellationToken.None
		);

		await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: CategoryType.Income,
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassIsArchivedFilterToRepository()
	{
		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7(), IsArchived: false),
			ct: CancellationToken.None
		);

		await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: false,
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassParentIdFilterToRepository()
	{
		Guid parentId = Guid.CreateVersion7();

		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7(), ParentId: parentId),
			ct: CancellationToken.None
		);

		await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: parentId,
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassCursorToRepository()
	{
		DateTimeOffset cursorCreatedAt = FakeDateProvider.Default.UtcNow;
		Guid cursorId = Guid.CreateVersion7();

		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetCategoriesQuery(
				UserId: Guid.CreateVersion7(),
				CursorCreatedAt: cursorCreatedAt,
				CursorId: cursorId
			),
			ct: CancellationToken.None
		);

		await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: cursorCreatedAt,
			cursorId: cursorId,
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassPageSizeToRepository()
	{
		_categoryReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7(), PageSize: 50),
			ct: CancellationToken.None
		);

		await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<CategoryType?>(),
			isArchived: Arg.Any<bool?>(),
			parentId: Arg.Any<Guid?>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: 50,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
