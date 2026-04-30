using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
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
		_handler = new ChangeTransactionCategoryHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			unitOfWork: _unitOfWork,
			budgetProgressWriteRepository: _budgetProgressWriteRepository
		);
	}

	[Test]
	public async Task HandleAsync_WithDebitNotExcluded_ShouldUpdateCategoryTotalsAndBudget()
	{
		TransactionDto transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);
		Guid newCategoryId = Guid.NewGuid();

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: newCategoryId),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: transaction.CategoryId,
			newCategoryId: newCategoryId,
			amount: transaction.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: transaction.CategoryId,
			newCategoryId: newCategoryId,
			currencyCode: transaction.Currency,
			amount: transaction.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithExcludedTransaction_ShouldNotUpdateTotals()
	{
		TransactionDto transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: true);

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.NewGuid()),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.DidNotReceive().ChangeCategoryAsync(
			userId: Arg.Any<Guid>(), oldCategoryId: Arg.Any<Guid>(), newCategoryId: Arg.Any<Guid>(),
			amount: Arg.Any<decimal>(), occurredAt: Arg.Any<DateTime>(), ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithCreditTransaction_ShouldNotUpdateTotals()
	{
		TransactionDto transaction = TransactionFactory.Create(direction: DirectionType.Credit, isExcluded: false);

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.NewGuid()),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.DidNotReceive().ChangeCategoryAsync(
			userId: Arg.Any<Guid>(), oldCategoryId: Arg.Any<Guid>(), newCategoryId: Arg.Any<Guid>(),
			amount: Arg.Any<decimal>(), occurredAt: Arg.Any<DateTime>(), ct: Arg.Any<CancellationToken>()
		);
	}
}