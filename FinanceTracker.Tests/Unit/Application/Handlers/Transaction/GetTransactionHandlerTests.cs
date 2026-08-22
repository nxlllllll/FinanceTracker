using FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class GetTransactionHandlerTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private GetTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_handler = new GetTransactionHandler(transactionReadRepository: _transactionReadRepository);
	}

	[Test]
	public async Task Handle_WhenTransactionExists_ShouldReturnSuccess()
	{
		TransactionReadModel model = TransactionFactory.CreateReadModel();
		GetTransactionQuery query = new GetTransactionQuery(
			TransactionId: model.Id,
			UserId: model.UserId
		);

		_transactionReadRepository
			.GetByIdAsync(transactionId: model.Id, userId: model.UserId, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: model);

		Result<TransactionReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenTransactionNotFound_ShouldReturnNotFound()
	{
		Guid transactionId = Guid.CreateVersion7();
		GetTransactionQuery query = new GetTransactionQuery(
			TransactionId: transactionId,
			UserId: Guid.CreateVersion7()
		);

		_transactionReadRepository
			.GetByIdAsync(transactionId: transactionId, userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: (TransactionReadModel?)null);

		Result<TransactionReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
