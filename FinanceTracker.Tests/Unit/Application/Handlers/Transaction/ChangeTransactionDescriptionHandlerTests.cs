using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionDescription;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Operations;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionDescriptionHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private IOperationsWriteRepository _operationsWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeTransactionDescriptionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_operationsWriteRepository = Substitute.For<IOperationsWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeTransactionDescriptionHandler(
			transactionWriteRepository: _transactionWriteRepository,
			operationsWriteRepository: _operationsWriteRepository,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task HandleAsync_WhenDescriptionChanges_ShouldCallRepository()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(description: "Old description");

		Result<Guid, FinanceTracker.Core.Exceptions.DomainExceptions.DomainException> result = await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "New description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ChangeDescriptionAsync(
			transactionId: transaction.Id,
			description: "New description",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenDescriptionIsSame_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(description: "Same description");

		Result<Guid, FinanceTracker.Core.Exceptions.DomainExceptions.DomainException> result = await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "Same description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _transactionWriteRepository.DidNotReceive().ChangeDescriptionAsync(
			transactionId: Arg.Any<Guid>(),
			description: Arg.Any<string?>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldReturnTransactionId()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create();

		Result<Guid, FinanceTracker.Core.Exceptions.DomainExceptions.DomainException> result = await _handler.HandleAsync(
			command: new ChangeTransactionDescriptionCommand(
				UserId: transaction.UserId,
				TransactionId: transaction.Id,
				Description: "New description"
			),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Value).IsEqualTo(expected: transaction.Id);
	}
}