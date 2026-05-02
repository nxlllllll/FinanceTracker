using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ExcludeTransactionHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ExcludeTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_handler = new ExcludeTransactionHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyExcluded_ShouldThrowExcludingException()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(isExcluded: true);

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new ExcludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		)).Throws<ExcludingException>();
	}

	[Test]
	public async Task HandleAsync_WithIncludedDebit_ShouldExcludeAndSubtractTotals()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);

		await _handler.HandleAsync(
			command: new ExcludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ExcludeAsync(
			transactionId: transaction.Id, ct: Arg.Any<CancellationToken>()
		);
		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).SubtractAsync(
			userId: transaction.UserId, 
			categoryId: transaction.CategoryId,
			currency: transaction.Amount.Currency,
			amount: transaction.Amount.Amount, 
			occurredAt: transaction.OccurredAt, 
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).SubtractAsync(
			userId: transaction.UserId, 
			categoryId: transaction.CategoryId,
			currencyCode: transaction.Amount.Currency, 
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt, 
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithIncludedCredit_ShouldExcludeButNotSubtractTotals()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Credit, isExcluded: false);

		await _handler.HandleAsync(
			command: new ExcludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ExcludeAsync(
			transactionId: transaction.Id, ct: Arg.Any<CancellationToken>()
		);
		await _categoryTotalWriteRepository.DidNotReceive().SubtractAsync(
			userId: Arg.Any<Guid>(), 
			categoryId: Arg.Any<Guid>(),
			amount: Arg.Any<decimal>(), 
			currency: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTime>(), 
			ct: Arg.Any<CancellationToken>()
		);
	}
}