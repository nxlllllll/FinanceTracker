using FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class GetRecurringTransactionsHandlerTests
{
	private IRecurringTransactionReadRepository _readRepository = null!;
	private GetRecurringTransactionsHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_handler = new GetRecurringTransactionsHandler(recurringTransactionReadRepository: _readRepository);
	}

	private static PagedResult<RecurringTransactionReadModel> EmptyPage()
	{
		return new PagedResult<RecurringTransactionReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<RecurringTransactionReadModel> PageOf(
		IReadOnlyList<RecurringTransactionReadModel> items)
	{
		return new PagedResult<RecurringTransactionReadModel>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllUserTransactions()
	{
		Guid userId = Guid.CreateVersion7();
		IReadOnlyList<RecurringTransactionReadModel> items = [
			RecurringTransactionFactory.CreateReadModel(userId: userId),
			RecurringTransactionFactory.CreateReadModel(userId: userId)
		];

		_readRepository.GetByUserIdAsync(
			userId: Arg.Any<Guid>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: items));

		Result<PagedResult<RecurringTransactionReadModel>, AppException> result = await _handler.Handle(
			query: new GetRecurringTransactionsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_WhenNoTransactions_ShouldReturnEmptyList()
	{
		_readRepository.GetByUserIdAsync(
			userId: Arg.Any<Guid>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		Result<PagedResult<RecurringTransactionReadModel>, AppException> result = await _handler.Handle(
			query: new GetRecurringTransactionsQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Items).IsEmpty();
	}
}
