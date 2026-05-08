using FinanceTracker.Application.UseCases.Users.Queries.GetIncomeExpenseSummary;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetIncomeExpenseSummaryHandlerTests
{
	private ICategoryTotalReadRepository _categoryTotalReadRepository = null!;
	private IUserReadRepository _userReadRepository = null!;
	private GetIncomeExpenseSummaryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryTotalReadRepository = Substitute.For<ICategoryTotalReadRepository>();
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_handler = new GetIncomeExpenseSummaryHandler(
			categoryTotalReadRepository: _categoryTotalReadRepository,
			userReadRepository: _userReadRepository
		);
	}

	[Test]
	public async Task Handle_ShouldReturnIncomeAndExpense()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;
		DateOnly period = new DateOnly(year: 2024, month: 1, day: 1);

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_categoryTotalReadRepository.GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Income: 10000m, Expense: 4000m));

		IncomeExpenseSummaryDto result = await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(UserId: user.Id, Period: period),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Income).IsEqualTo(expected: 10000m);
		await Assert.That(value: result.Expense).IsEqualTo(expected: 4000m);
		await Assert.That(value: result.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: result.Period).IsEqualTo(expected: period);
	}

	[Test]
	public async Task Handle_WithNoTransactions_ShouldReturnZeros()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(baseCurrencyCode: "RUB").Value!;
		DateOnly period = new DateOnly(year: 2024, month: 1, day: 1);

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_categoryTotalReadRepository.GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Income: 0m, Expense: 0m));

		IncomeExpenseSummaryDto result = await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(UserId: user.Id, Period: period),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Income).IsEqualTo(expected: 0m);
		await Assert.That(value: result.Expense).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task Handle_ShouldPassPeriodToRepository()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		DateOnly period = new DateOnly(year: 2024, month: 6, day: 1);

		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_categoryTotalReadRepository.GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Income: 0m, Expense: 0m));

		await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(UserId: user.Id, Period: period),
			ct: CancellationToken.None
		);

		await _categoryTotalReadRepository.Received(requiredNumberOfCalls: 1).GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: period,
			ct: Arg.Any<CancellationToken>()
		);
	}
}