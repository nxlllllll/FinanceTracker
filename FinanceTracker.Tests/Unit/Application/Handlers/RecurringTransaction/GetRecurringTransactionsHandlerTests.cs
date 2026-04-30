using FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransactions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
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
		_handler = new GetRecurringTransactionsHandler(
			recurringTransactionReadRepository: _readRepository
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllUserTransactions()
	{
		Guid userId = Guid.NewGuid();
		List<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> dtos = [RecurringTransactionFactory.Create(userId: userId), RecurringTransactionFactory.Create(userId: userId)];
		_readRepository.GetByUserIdAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: dtos);

		IReadOnlyList<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> result = await _handler.Handle(
			query: new GetRecurringTransactionsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_WhenNoTransactions_ShouldReturnEmptyList()
	{
		Guid userId = Guid.NewGuid();
		_readRepository.GetByUserIdAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new List<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>());

		IReadOnlyList<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction> result = await _handler.Handle(
			query: new GetRecurringTransactionsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 0);
	}
}