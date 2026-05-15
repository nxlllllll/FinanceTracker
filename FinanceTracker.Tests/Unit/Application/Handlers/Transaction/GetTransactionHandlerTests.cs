using FinanceTracker.Application.UseCases.Transactions.Queries.GetTransaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class GetTransactionHandlerTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private GetTransactionHandler _handler = null!;

	private static readonly Guid UserId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_handler = new GetTransactionHandler(transactionReadRepository: _transactionReadRepository);
	}

	[Test]
	public async Task Handle_WhenTransactionExists_ShouldReturnTransaction()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create();

		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transaction);

		FinanceTracker.Core.Domains.Transaction.Transaction? result = await _handler.Handle(
			query: new GetTransactionQuery(TransactionId: transaction.Id, UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: transaction.Id);
	}

	[Test]
	public async Task Handle_WhenTransactionNotFound_ShouldReturnNull()
	{
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Transaction.Transaction?)null);

		FinanceTracker.Core.Domains.Transaction.Transaction? result = await _handler.Handle(
			query: new GetTransactionQuery(TransactionId: Guid.CreateVersion7(), UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task Handle_ShouldPassBothTransactionIdAndUserIdToRepository()
	{
		Guid transactionId = Guid.CreateVersion7();

		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.Transaction.Transaction?)null);

		await _handler.Handle(
			query: new GetTransactionQuery(TransactionId: transactionId, UserId: UserId),
			ct: CancellationToken.None
		);

		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			transactionId: transactionId,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		);
	}
}