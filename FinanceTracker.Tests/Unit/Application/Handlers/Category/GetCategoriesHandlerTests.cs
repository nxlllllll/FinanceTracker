using FinanceTracker.Application.UseCases.Category.Queries.GetCategories;
using FinanceTracker.Core.Domains.Category;
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

	private static PagedResult<FinanceTracker.Core.Domains.Category.Category> EmptyPage()
	{
		return new PagedResult<FinanceTracker.Core.Domains.Category.Category>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<FinanceTracker.Core.Domains.Category.Category> PageOf(IReadOnlyList<FinanceTracker.Core.Domains.Category.Category> items)
	{
		return new PagedResult<FinanceTracker.Core.Domains.Category.Category>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllCategories()
	{
		IReadOnlyList<FinanceTracker.Core.Domains.Category.Category> categories = [
			CategoryFactory.Create().Value!,
			CategoryFactory.Create().Value!
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

		PagedResult<FinanceTracker.Core.Domains.Category.Category> result = await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
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

		PagedResult<FinanceTracker.Core.Domains.Category.Category> result = await _handler.Handle(
			query: new GetCategoriesQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items).IsEmpty();
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
