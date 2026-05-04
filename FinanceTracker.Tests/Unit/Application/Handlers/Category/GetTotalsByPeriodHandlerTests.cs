using FinanceTracker.Application.UseCases.Categories.Queries.GetTotalsByPeriod;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class GetTotalsByPeriodHandlerTests
{
    private ICategoryTotalReadRepository _categoryTotalReadRepository = null!;
    private GetTotalsByPeriodHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _categoryTotalReadRepository = Substitute.For<ICategoryTotalReadRepository>();
        _handler = new GetTotalsByPeriodHandler(categoryTotalReadRepository: _categoryTotalReadRepository);
    }

    [Test]
    public async Task Handle_ShouldReturnAllTotalsForPeriod()
    {
        Guid userId = Guid.NewGuid();
        DateOnly period = new DateOnly(year: 2025, month: 1, day: 1);

        IReadOnlyList<CategoryTotalDto> totals =
        [
            new CategoryTotalDto(
                CategoryId: Guid.NewGuid(),
                Period: period,
                Total: 1000m,
                Count: 1,
                UpdatedAt: FakeDateProvider.Default.UtcNow
            ),
            new CategoryTotalDto(
                CategoryId: Guid.NewGuid(),
                Period: period,
                Total: 2000m,
                Count: 2,
                UpdatedAt: FakeDateProvider.Default.UtcNow
            ),
        ];

        _categoryTotalReadRepository.GetAllByPeriodAsync(
            userId: userId,
            period: period,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: totals);

        IReadOnlyList<CategoryTotalDto> result = await _handler.Handle(
            query: new GetTotalsByPeriodQuery(UserId: userId, Period: period),
            ct: CancellationToken.None
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Handle_WhenNoTotalsExist_ShouldReturnEmptyList()
    {
        _categoryTotalReadRepository.GetAllByPeriodAsync(
            userId: Arg.Any<Guid>(),
            period: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        IReadOnlyList<CategoryTotalDto> result = await _handler.Handle(
            query: new GetTotalsByPeriodQuery(
                UserId: Guid.NewGuid(),
                Period: new DateOnly(year: 2025, month: 1, day: 1)
            ),
            ct: CancellationToken.None
        );

        await Assert.That(value: result).IsEmpty();
    }
}