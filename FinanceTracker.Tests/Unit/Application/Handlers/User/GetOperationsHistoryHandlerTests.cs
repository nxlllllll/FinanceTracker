using FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetOperationsHistoryHandlerTests
{
	private IUserReadRepository _userReadRepository = null!;
	private GetOperationsHistoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_handler = new GetOperationsHistoryHandler(userReadRepository: _userReadRepository);
	}

	private static PagedResult<OperationDto> EmptyPage()
	{
		return new PagedResult<OperationDto>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<OperationDto> PageOf(IReadOnlyList<OperationDto> items)
	{
		return new PagedResult<OperationDto>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnOperations()
	{
		IReadOnlyList<OperationDto> operations = [
			new OperationDto(
				Id: Guid.CreateVersion7(),
				Type: OperationFilterType.Income,
				Description: null,
				OccurredAt: FakeDateProvider.Default.UtcNow,
				Transaction: new TransactionDetailsDto(
					AccountId: Guid.CreateVersion7(),
					CategoryId: Guid.CreateVersion7(),
					Amount: 1000m,
					Currency: Currency.Create(value: "RUB").Value,
					Direction: DirectionType.Credit,
					IsExcluded: false
				),
				Transfer: null
			)
		];

		_userReadRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTime?>(),
			dateTo: Arg.Any<DateTime?>(),
			cursorOccurredAt: Arg.Any<DateTime?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: operations));

		PagedResult<OperationDto> result = await _handler.Handle(
			query: new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_ShouldPassAllFiltersToRepository()
	{
		Guid userId = Guid.CreateVersion7();
		DateTime dateFrom = FakeDateProvider.Default.UtcNow.AddDays(value: -30);
		DateTime dateTo = FakeDateProvider.Default.UtcNow;
		DateTime cursorOccurredAt = FakeDateProvider.Default.UtcNow.AddDays(value: -1);
		Guid cursorId = Guid.CreateVersion7();

		_userReadRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTime?>(),
			dateTo: Arg.Any<DateTime?>(),
			cursorOccurredAt: Arg.Any<DateTime?>(),
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

		await _userReadRepository.Received(requiredNumberOfCalls: 1).GetHistoryAsync(
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
		_userReadRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTime?>(),
			dateTo: Arg.Any<DateTime?>(),
			cursorOccurredAt: Arg.Any<DateTime?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		PagedResult<OperationDto> result = await _handler.Handle(
			query: new GetOperationsHistoryQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items).IsEmpty();
	}
}