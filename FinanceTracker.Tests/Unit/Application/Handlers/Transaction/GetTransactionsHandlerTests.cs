using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransactions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class GetTransactionsHandlerTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private GetTransactionsHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_handler = new GetTransactionsHandler(transactionReadRepository: _transactionReadRepository);
	}

	private static PagedResult<TransactionReadModel> EmptyPage()
	{
		return new PagedResult<TransactionReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<TransactionReadModel> PageOf(IReadOnlyList<TransactionReadModel> items)
	{
		return new PagedResult<TransactionReadModel>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllTransactions()
	{
		Guid userId = Guid.CreateVersion7();
		Guid accountId = Guid.CreateVersion7();
		IReadOnlyList<TransactionReadModel> transactions = [
			TransactionFactory.CreateReadModel(userId: userId, accountId: accountId),
			TransactionFactory.CreateReadModel(userId: userId, accountId: accountId)
		];

		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: transactions));

		PagedResult<TransactionReadModel> result = await _handler.Handle(
			query: new GetTransactionsQuery(UserId: userId, AccountId: accountId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_ShouldPassCategoryIdFilterToRepository()
	{
		Guid categoryId = Guid.CreateVersion7();

		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetTransactionsQuery(UserId: Guid.CreateVersion7(), AccountId: Guid.CreateVersion7(), CategoryId: categoryId),
			ct: CancellationToken.None
		);

		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: categoryId,
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassDirectionFilterToRepository()
	{
		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetTransactionsQuery(UserId: Guid.CreateVersion7(), AccountId: Guid.CreateVersion7(), Direction: DirectionType.Credit),
			ct: CancellationToken.None
		);

		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: DirectionType.Credit,
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassIsExcludedFilterToRepository()
	{
		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(
			query: new GetTransactionsQuery(UserId: Guid.CreateVersion7(), AccountId: Guid.CreateVersion7(), IsExcluded: false),
			ct: CancellationToken.None
		);

		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: false,
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_ShouldPassDateRangeFilterToRepository()
	{
		DateTimeOffset dateFrom = FakeDateProvider.Default.UtcNow.AddDays(days: -7);
		DateTimeOffset dateTo = FakeDateProvider.Default.UtcNow;

		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(query: new GetTransactionsQuery(
			UserId: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			DateFrom: dateFrom,
			DateTo: dateTo
		), ct: CancellationToken.None);

		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: dateFrom,
			dateTo: dateTo,
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenNoTransactions_ShouldReturnEmptyList()
	{
		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		PagedResult<TransactionReadModel> result = await _handler.Handle(
			query: new GetTransactionsQuery(UserId: Guid.CreateVersion7(), AccountId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items).IsEmpty();
	}

	[Test]
	public async Task Handle_ShouldPassAllFiltersToRepository()
	{
		Guid userId = Guid.CreateVersion7();
		Guid accountId = Guid.CreateVersion7();
		Guid categoryId = Guid.CreateVersion7();
		DateTimeOffset dateFrom = FakeDateProvider.Default.UtcNow.AddDays(days: -30);
		DateTimeOffset dateTo = FakeDateProvider.Default.UtcNow;

		_transactionReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			accountId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid?>(),
			direction: Arg.Any<DirectionType?>(),
			isExcluded: Arg.Any<bool?>(),
			dateFrom: Arg.Any<DateTimeOffset?>(),
			dateTo: Arg.Any<DateTimeOffset?>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		await _handler.Handle(query: new GetTransactionsQuery(
			UserId: userId,
			AccountId: accountId,
			CategoryId: categoryId,
			Direction: DirectionType.Debit,
			IsExcluded: false,
			DateFrom: dateFrom,
			DateTo: dateTo
		), ct: CancellationToken.None);

		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			direction: DirectionType.Debit,
			isExcluded: false,
			dateFrom: dateFrom,
			dateTo: dateTo,
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
