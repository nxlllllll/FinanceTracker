using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class IncludeTransactionHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IncludeTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_handler = new IncludeTransactionHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyIncluded_ShouldThrowIncludingException()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(isExcluded: false);

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new IncludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		)).Throws<IncludingException>();
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
			currency: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTime>(), 
			ct: Arg.Any<CancellationToken>()
		);
	}
}