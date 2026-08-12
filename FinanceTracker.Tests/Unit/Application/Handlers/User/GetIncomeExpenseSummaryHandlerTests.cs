using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetIncomeExpenseSummaryHandlerTests
{
	private IUserQueryRepository _userQueryRepository = null!;
	private GetIncomeExpenseSummaryHandler _handler = null!;
	private IBaseCurrencyRecalculationReadRepository _recalculationReadRepository = null!;

	private static UserReadModel CreateUserReadModel(string currency = "RUB") => new UserReadModel(
		Id: Guid.CreateVersion7(),
		Email: Email.Create(value: "test@test.com").Value!,
		BaseCurrency: FinanceTracker.Core.ValueObjects.Currency.Create(value: currency).Value,
		CreatedAt: FakeDateProvider.Default.UtcNow
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userQueryRepository = Substitute.For<IUserQueryRepository>();
		_recalculationReadRepository = Substitute.For<IBaseCurrencyRecalculationReadRepository>();

		_handler = new GetIncomeExpenseSummaryHandler(
			userQueryRepository: _userQueryRepository,
			recalculationReadRepository: _recalculationReadRepository
		);
	}

	[Test]
	public async Task Handle_ShouldReturnIncomeAndExpense()
	{
		UserReadModel user = CreateUserReadModel(currency: "RUB");
		DateOnly period = new DateOnly(year: 2024, month: 1, day: 1);

		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_userQueryRepository.GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Income: 10000m, Expense: 4000m));

		Result<IncomeExpenseSummary, AppException> result = await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(UserId: user.Id, Period: period),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Income).IsEqualTo(expected: 10000m);
		await Assert.That(value: result.Value?.Expense).IsEqualTo(expected: 4000m);
		await Assert.That(value: result.Value?.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: result.Value?.Period).IsEqualTo(expected: period);
	}

	[Test]
	public async Task Handle_WithNoTransactions_ShouldReturnZeros()
	{
		UserReadModel user = CreateUserReadModel();
		DateOnly period = new DateOnly(year: 2024, month: 1, day: 1);

		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_userQueryRepository.GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Income: 0m, Expense: 0m));

		Result<IncomeExpenseSummary, AppException> result = await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(UserId: user.Id, Period: period),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Income).IsEqualTo(expected: 0m);
		await Assert.That(value: result.Value?.Expense).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task Handle_ShouldPassPeriodToRepository()
	{
		UserReadModel user = CreateUserReadModel();
		DateOnly period = new DateOnly(year: 2024, month: 6, day: 1);

		_userQueryRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_userQueryRepository.GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Income: 0m, Expense: 0m));

		await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(UserId: user.Id, Period: period),
			ct: CancellationToken.None
		);

		await _userQueryRepository.Received(requiredNumberOfCalls: 1).GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: period,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhileTotalsAreBeingRebuilt_ShouldReportZeroesWithTheFlagSet()
	{
		UserReadModel user = CreateUserReadModel();

		_userQueryRepository.GetByIdAsync(
			userId: user.Id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		_recalculationReadRepository.TotalsAreUnavailableAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		Result<IncomeExpenseSummary, AppException> result = await _handler.Handle(
			query: new GetIncomeExpenseSummaryQuery(
				UserId: user.Id,
				Period: new DateOnly(year: 2026, month: 8, day: 1)
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.RecalculationPending).IsTrue();
		await Assert.That(value: result.Value!.Income).IsEqualTo(expected: 0m);
		await Assert.That(value: result.Value!.Expense).IsEqualTo(expected: 0m).Because(message: """
			The summary is labelled with the user's current currency. Filling it with amounts built
			from the previous one would put a correct label on wrong numbers.
		""");

		await _userQueryRepository.DidNotReceive().GetIncomeExpenseSummaryAsync(
			userId: Arg.Any<Guid>(),
			period: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
