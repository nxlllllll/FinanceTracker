using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionCategoryHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeTransactionCategoryHandler _handler = null!;

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
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
		_handler = new ChangeTransactionCategoryHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			unitOfWork: _unitOfWork,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			logger: Substitute.For<ILogger<ChangeTransactionCategoryHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithDebitNotExcluded_ShouldUpdateCategoryTotalsAndBudget()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);
		Guid newCategoryId = Guid.CreateVersion7();

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: newCategoryId),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: transaction.CategoryId,
			newCategoryId: newCategoryId,
			currency: transaction.Amount.Currency,
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: transaction.CategoryId,
			newCategoryId: newCategoryId,
			currencyCode: transaction.Amount.Currency,
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithExcludedTransaction_ShouldNotUpdateTotals()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: true);

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.CreateVersion7()),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.DidNotReceive().ChangeCategoryAsync(
			userId: Arg.Any<Guid>(),
			oldCategoryId: Arg.Any<Guid>(),
			newCategoryId: Arg.Any<Guid>(),
			currency: transaction.Amount.Currency,
			amount: Arg.Any<decimal>(),
			occurredAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithCreditTransaction_ShouldNotUpdateTotals()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Credit, isExcluded: false);

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.CreateVersion7()),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.DidNotReceive().ChangeCategoryAsync(
			userId: Arg.Any<Guid>(), 
			oldCategoryId: Arg.Any<Guid>(), 
			newCategoryId: Arg.Any<Guid>(),
			currency: transaction.Amount.Currency,
			amount: Arg.Any<decimal>(), 
			occurredAt: Arg.Any<DateTime>(), 
			ct: Arg.Any<CancellationToken>()
		);
	}
}