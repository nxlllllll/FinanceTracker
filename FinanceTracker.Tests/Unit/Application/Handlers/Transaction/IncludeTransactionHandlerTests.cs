using FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Operation;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class IncludeTransactionHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IOperationsWriteRepository _operationsWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IncludeTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_operationsWriteRepository = Substitute.For<IOperationsWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
		_handler = new IncludeTransactionHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			operationsWriteRepository: _operationsWriteRepository,
			unitOfWork: _unitOfWork,
			logger: Substitute.For<ILogger<IncludeTransactionHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyIncluded_ShouldThrowIncludingException()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(isExcluded: false);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new IncludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IncludingException>();
	}

	[Test]
	public async Task HandleAsync_WithExcludedDebit_ShouldIncludeAndAddTotals()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: true);

		await _handler.HandleAsync(
			command: new IncludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).IncludeAsync(
			transactionId: transaction.Id, ct: Arg.Any<CancellationToken>()
		);
		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
			userId: transaction.UserId, 
			categoryId: transaction.CategoryId,
			currency: transaction.Amount.Currency,
			amount: transaction.Amount.Amount, 
			occurredAt: transaction.OccurredAt, 
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
			userId: transaction.UserId, 
			categoryId: transaction.CategoryId,
			currencyCode: transaction.Amount.Currency,
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithExcludedCredit_ShouldIncludeButNotUpdateTotals()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Credit, isExcluded: true);

		await _handler.HandleAsync(
			command: new IncludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).IncludeAsync(
			transactionId: transaction.Id, ct: Arg.Any<CancellationToken>()
		);
		await _categoryTotalWriteRepository.DidNotReceive().AddAsync(
			userId: Arg.Any<Guid>(), 
			categoryId: Arg.Any<Guid>(),
			amount: Arg.Any<decimal>(), 
			currency: Arg.Any<Currency>(),
			occurredAt: Arg.Any<DateTimeOffset>(), 
			ct: Arg.Any<CancellationToken>()
		);
	}
}
