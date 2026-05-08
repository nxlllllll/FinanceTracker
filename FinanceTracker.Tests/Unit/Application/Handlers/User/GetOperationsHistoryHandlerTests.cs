using FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Operations;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class GetOperationsHistoryHandlerTests
{
	private IOperationsReadRepository _operationsReadRepository = null!;
	private GetOperationsHistoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_operationsReadRepository = Substitute.For<IOperationsReadRepository>();
		_handler = new GetOperationsHistoryHandler(operationsReadRepository: _operationsReadRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnOperations()
	{
		IReadOnlyList<OperationDto> operations =
		[
			new OperationDto(
				Id: Guid.NewGuid(),
				Type: OperationFilterType.Income,
				Description: null,
				OccurredAt: FakeDateProvider.Default.UtcNow,
				Transaction: new TransactionDetailsDto(
					AccountId: Guid.NewGuid(),
					CategoryId: Guid.NewGuid(),
					Amount: 1000m,
					Currency: Currency.Create(value: "RUB").Value,
					Direction: DirectionType.Credit,
					IsExcluded: false
				),
				Transfer: null
			)
		];

		_operationsReadRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTime?>(),
			dateTo: Arg.Any<DateTime?>(),
			cursorOccurredAt: Arg.Any<DateTime?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: operations);

		IReadOnlyList<OperationDto> result = await _handler.Handle(
			query: new GetOperationsHistoryQuery(UserId: Guid.NewGuid()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Handle_ShouldPassAllFiltersToRepository()
	{
		Guid userId = Guid.NewGuid();
		DateTime dateFrom = FakeDateProvider.Default.UtcNow.AddDays(value: -30);
		DateTime dateTo = FakeDateProvider.Default.UtcNow;
		DateTime cursorOccurredAt = FakeDateProvider.Default.UtcNow.AddDays(value: -1);
		Guid cursorId = Guid.NewGuid();

		_operationsReadRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTime?>(),
			dateTo: Arg.Any<DateTime?>(),
			cursorOccurredAt: Arg.Any<DateTime?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		await _handler.Handle(query: new GetOperationsHistoryQuery(
			UserId: userId,
			Type: OperationFilterType.Expense,
			DateFrom: dateFrom,
			DateTo: dateTo,
			CursorOccurredAt: cursorOccurredAt,
			CursorId: cursorId,
			PageSize: 50
		), ct: CancellationToken.None);

		await _operationsReadRepository.Received(requiredNumberOfCalls: 1).GetHistoryAsync(
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
		_operationsReadRepository.GetHistoryAsync(
			userId: Arg.Any<Guid>(),
			type: Arg.Any<OperationFilterType?>(),
			dateFrom: Arg.Any<DateTime?>(),
			dateTo: Arg.Any<DateTime?>(),
			cursorOccurredAt: Arg.Any<DateTime?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		IReadOnlyList<OperationDto> result = await _handler.Handle(
			query: new GetOperationsHistoryQuery(UserId: Guid.NewGuid()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsEmpty();
	}
}