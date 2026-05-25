using FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransactions;
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

	private static PagedResult<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> EmptyPage()
	{
		return new PagedResult<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> PageOf(
		IReadOnlyList<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> items)
	{
		return new PagedResult<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>(
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
		IReadOnlyList<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> items = [
			RecurringTransactionFactory.Create(userId: userId).Value!,
			RecurringTransactionFactory.Create(userId: userId).Value!
		];

		_readRepository.GetByUserIdAsync(
			userId: Arg.Any<Guid>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: items));

		PagedResult<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> result = await _handler.Handle(
			query: new GetRecurringTransactionsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
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

		PagedResult<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> result = await _handler.Handle(
			query: new GetRecurringTransactionsQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items).IsEmpty();
	}
}
