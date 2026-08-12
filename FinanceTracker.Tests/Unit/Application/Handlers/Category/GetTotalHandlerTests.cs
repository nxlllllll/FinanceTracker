using FinanceTracker.Application.UseCases.Category.Queries.GetTotal;
using FinanceTracker.Application.UseCases.Category.Queries.GetTotalsByPeriod;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Category;

public sealed class GetTotalHandlerTests
{
	private ICategoryTotalReadRepository _categoryTotalReadRepository = null!;
	private GetTotalHandler _handler = null!;
	private IBaseCurrencyRecalculationReadRepository _recalculationReadRepository = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryTotalReadRepository = Substitute.For<ICategoryTotalReadRepository>();

		_recalculationReadRepository = Substitute.For<IBaseCurrencyRecalculationReadRepository>();
		_recalculationReadRepository.TotalsAreUnavailableAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		_handler = new GetTotalHandler(
			categoryTotalReadRepository: _categoryTotalReadRepository,
			recalculationReadRepository: _recalculationReadRepository
		);
	}

	[Test]
	public async Task Handle_WhenTotalExists_ShouldReturnCategoryTotalDto()
	{
		Guid userId = Guid.CreateVersion7();
		Guid categoryId = Guid.CreateVersion7();
		DateOnly period = new DateOnly(year: 2025, month: 1, day: 1);

		CategoryTotal dto = new CategoryTotal(
			CategoryId: categoryId,
			Period: period,
			Total: 5000m,
			Count: 3,
			UpdatedAt: FakeDateProvider.Default.UtcNow
		);

		_categoryTotalReadRepository.GetByCategoryAsync(
			userId: userId,
			categoryId: categoryId,
			period: period,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: dto);

		Result<CategoryTotalView, AppException> result = await _handler.Handle(
			query: new GetTotalQuery(UserId: userId, CategoryId: categoryId, Period: period),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotNull();
		await Assert.That(value: result.Value.Total!.Total).IsEqualTo(expected: 5000m);
		await Assert.That(value: result.Value.Total.CategoryId).IsEqualTo(expected: categoryId);
		await Assert.That(value: result.Value.Total.Count).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task Handle_WhenTotalNotFound_ShouldReturnEmptyTotal()
	{
		_categoryTotalReadRepository.GetByCategoryAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<CategoryTotal?>(result: null));

		Result<CategoryTotalView, AppException> result = await _handler.Handle(
			query: new GetTotalQuery(
				UserId: Guid.CreateVersion7(),
				CategoryId: Guid.CreateVersion7(),
				Period: new DateOnly(year: 2025, month: 1, day: 1)
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotNull();
		await Assert.That(value: result.Value.Total!.Total).IsEqualTo(expected: 0);
		await Assert.That(value: result.Value.Total.Count).IsEqualTo(expected: 0);
		await Assert.That(value: result.Value.Total.UpdatedAt).IsNull();
	}

	[Test]
	public async Task Handle_WhileTotalsAreBeingRebuilt_ShouldReturnNoAmountWithTheFlagSet()
	{
		_recalculationReadRepository.TotalsAreUnavailableAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		Result<CategoryTotalView, AppException> result = await _handler.Handle(
			query: new GetTotalQuery(
				UserId: Guid.CreateVersion7(),
				CategoryId: Guid.CreateVersion7(),
				Period: new DateOnly(year: 2026, month: 8, day: 1)
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Value!.RecalculationPending).IsTrue();
		await Assert.That(value: result.Value!.Total).IsNull().Because(message: """
			Null here means the amount is not knowable yet. A category with no activity reports zero,
			and collapsing the two would present an unfinished rebuild as an empty month.
		""");
	}
}
