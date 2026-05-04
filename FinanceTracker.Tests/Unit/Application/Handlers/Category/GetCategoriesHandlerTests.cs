using FinanceTracker.Application.UseCases.Categories.Queries.GetCategories;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
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
        _handler = new GetCategoriesHandler(categoryRepository: _categoryReadRepository);
    }

    [Test]
    public async Task Handle_ShouldReturnAllCategories()
    {
        IReadOnlyList<FinanceTracker.Core.Domains.Category.Category> categories = 
        [
            CategoryFactory.Create().Value!,
            CategoryFactory.Create().Value!
        ];

        _categoryReadRepository.GetAllAsync(
            userId: Arg.Any<Guid>(),
            type: Arg.Any<CategoryType?>(),
            isArchived: Arg.Any<bool?>(),
            parentId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: categories);

        GetCategoriesQuery query = new GetCategoriesQuery(UserId: Guid.NewGuid());
        IReadOnlyList<FinanceTracker.Core.Domains.Category.Category> result = await _handler.Handle(query: query, ct: CancellationToken.None);

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Handle_ShouldPassTypeFilterToRepository()
    {
        _categoryReadRepository.GetAllAsync(
            userId: Arg.Any<Guid>(),
            type: Arg.Any<CategoryType?>(),
            isArchived: Arg.Any<bool?>(),
            parentId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetCategoriesQuery query = new GetCategoriesQuery(UserId: Guid.NewGuid(), Type: CategoryType.Income);

        await _handler.Handle(query: query, ct: CancellationToken.None);

        await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            userId: Arg.Any<Guid>(),
            type: CategoryType.Income,
            isArchived: Arg.Any<bool?>(),
            parentId: Arg.Any<Guid?>(),
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
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetCategoriesQuery query = new GetCategoriesQuery(UserId: Guid.NewGuid(), IsArchived: false);

        await _handler.Handle(query: query, ct: CancellationToken.None);

        await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            userId: Arg.Any<Guid>(),
            type: Arg.Any<CategoryType?>(),
            isArchived: false,
            parentId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_ShouldPassParentIdFilterToRepository()
    {
        Guid parentId = Guid.NewGuid();

        _categoryReadRepository.GetAllAsync(
            userId: Arg.Any<Guid>(),
            type: Arg.Any<CategoryType?>(),
            isArchived: Arg.Any<bool?>(),
            parentId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        GetCategoriesQuery query = new GetCategoriesQuery(UserId: Guid.NewGuid(), ParentId: parentId);

        await _handler.Handle(query: query, ct: CancellationToken.None);

        await _categoryReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
            userId: Arg.Any<Guid>(),
            type: Arg.Any<CategoryType?>(),
            isArchived: Arg.Any<bool?>(),
            parentId: parentId,
            ct: Arg.Any<CancellationToken>()
        );
    }
}