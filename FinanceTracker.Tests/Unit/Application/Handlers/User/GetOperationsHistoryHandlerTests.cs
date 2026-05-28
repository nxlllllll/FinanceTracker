using FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetOperationsHistoryHandlerTests
{
	private IUserQueryRepository _userQueryRepository = null!;
	private GetOperationsHistoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userQueryRepository = Substitute.For<IUserQueryRepository>();
		_handler = new GetOperationsHistoryHandler(userQueryRepository: _userQueryRepository);
	}

	private static PagedResult<Operation> EmptyPage()
	{
		return new PagedResult<Operation>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<Operation> PageOf(IReadOnlyList<Operation> items)
	{
		return new PagedResult<Operation>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnOperations()
	{
		IReadOnlyList<Operation> operations = [new Operation(
			Id: Guid.CreateVersion7(),
			Type: OperationFilterType.Income,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow,
			Transaction: new TransactionDetails(
				AccountId: Guid.CreateVersion7(),
				CategoryId: Guid.CreateVersion7(),
				Amount: 1000m,
				Currency: Currency.Create(value: "RUB").Value,
				Direction: DirectionType.Credit,
				IsExcluded: false
			),
			Transfer: null
		)];

		_userQueryRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: operations));

		PagedResult<Operation> result = await _handler.Handle(
			query: new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_ShouldPassAllFiltersToRepository()
	{
		Guid userId = Guid.CreateVersion7();
		DateTimeOffset dateFrom = FakeDateProvider.Default.UtcNow.AddDays(days: -30);
		DateTimeOffset dateTo = FakeDateProvider.Default.UtcNow;
		DateTimeOffset cursorOccurredAt = FakeDateProvider.Default.UtcNow.AddDays(days: -1);
		Guid cursorId = Guid.CreateVersion7();

		_userQueryRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(query: new GetOperationsHistoryQuery(
			UserId: userId,
			Type: OperationFilterType.Expense,
			DateFrom: dateFrom,
			DateTo: dateTo,
			CursorOccurredAt: cursorOccurredAt,
			CursorId: cursorId,
			PageSize: 50
		), ct: CancellationToken.None);

		await _userQueryRepository.Received(requiredNumberOfCalls: 1).GetHistoryAsync(
			userId: userId,
			type: OperationFilterType.Expense,
			dateFrom: dateFrom,
			dateTo: dateTo,
			cursorOccurredAt: cursorOccurredAt,
			cursorId: cursorId,
			pageSize: 50,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoOperations_ShouldReturnEmptyList()
	{
		_userQueryRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		PagedResult<Operation> result = await _handler.Handle(
			query: new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items).IsEmpty();
	}
}
