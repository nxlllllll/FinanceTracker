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

public sealed class GetTotalsByPeriodHandlerTests
{
	private ICategoryTotalReadRepository _categoryTotalReadRepository = null!;
	private GetTotalsByPeriodHandler _handler = null!;
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

		_handler = new GetTotalsByPeriodHandler(
			categoryTotalReadRepository: _categoryTotalReadRepository,
			recalculationReadRepository: _recalculationReadRepository
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllTotalsForPeriod()
	{
		Guid userId = Guid.CreateVersion7();
		DateOnly period = new DateOnly(year: 2025, month: 1, day: 1);

		IReadOnlyList<CategoryTotal> totals =
		[
			new CategoryTotal(
				CategoryId: Guid.CreateVersion7(),
				Period: period,
				Total: 1000m,
				Count: 1,
				UpdatedAt: FakeDateProvider.Default.UtcNow
			),
			new CategoryTotal(
				CategoryId: Guid.CreateVersion7(),
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

		Result<CategoryTotalsView, AppException> result = await _handler.Handle(
			query: new GetTotalsByPeriodQuery(UserId: userId, Period: period),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Totals.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_WhenNoTotalsExist_ShouldReturnEmptyList()
	{
		_categoryTotalReadRepository.GetAllByPeriodAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		Result<CategoryTotalsView, AppException> result = await _handler.Handle(
			query: new GetTotalsByPeriodQuery(
				UserId: Guid.CreateVersion7(),
				Period: new DateOnly(year: 2025, month: 1, day: 1)
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Totals).IsEmpty();
	}

	[Test]
	public async Task Handle_WhileTotalsAreBeingRebuilt_ShouldReturnNothingWithTheFlagSet()
	{
		_recalculationReadRepository.TotalsAreUnavailableAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		Result<CategoryTotalsView, AppException> result = await _handler.Handle(
			query: new GetTotalsByPeriodQuery(
				UserId: Guid.CreateVersion7(),
				Period: new DateOnly(year: 2026, month: 8, day: 1)
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Value!.RecalculationPending).IsTrue();
		await Assert.That(value: result.Value!.Totals).IsEmpty().Because(message: """
			The stored amounts are still in the currency the user moved off. Returning them would be
			worse than returning nothing: they are readable and plausible and off by an exchange rate.
		""");

		await _categoryTotalReadRepository.DidNotReceive().GetAllByPeriodAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
