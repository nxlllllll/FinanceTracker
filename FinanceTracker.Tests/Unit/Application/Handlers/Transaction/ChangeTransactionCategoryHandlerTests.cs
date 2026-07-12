using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionCategoryHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ICategoryReadRepository _categoryReadRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private ChangeTransactionCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_publisher = Substitute.For<IPublisher>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());

		SetupNewCategory(type: CategoryType.Expense);

		_handler = new ChangeTransactionCategoryHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryReadRepository: _categoryReadRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			unitOfWork: _unitOfWork,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ChangeTransactionCategoryHandler>>()
		);
	}

	private void SetupNewCategory(CategoryType type)
	{
		CategoryReadModel category = CategoryFactory.CreateReadModel(type: type);
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);
	}

	[Test]
	public async Task HandleAsync_WithDebitNotExcluded_ShouldUpdateCategoryTotalsAndBudget()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);
		Guid oldCategoryId = transaction.CategoryId;
		Guid newCategoryId = Guid.CreateVersion7();

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: newCategoryId),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currency: transaction.Amount.Currency,
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currencyCode: transaction.Amount.Currency,
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithDebitNotExcluded_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);
		Guid oldCategoryId = transaction.CategoryId;
		Guid newCategoryId = Guid.CreateVersion7();

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: newCategoryId),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<TransactionCategoryChangedNotification>(n =>
				n.TransactionId == transaction.Id &&
				n.UserId == transaction.UserId &&
				n.OldCategoryId == oldCategoryId &&
				n.NewCategoryId == newCategoryId),
			cancellationToken: Arg.Any<CancellationToken>()
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
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithCreditTransaction_ShouldUpdateCategoryTotalButNotBudgetProgress()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Credit, isExcluded: false);
		Guid oldCategoryId = transaction.CategoryId;
		Guid newCategoryId = Guid.CreateVersion7();
		SetupNewCategory(type: CategoryType.Income);

		await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: newCategoryId),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currency: transaction.Amount.Currency,
			amount: transaction.Amount.Amount,
			occurredAt: transaction.OccurredAt,
			ct: Arg.Any<CancellationToken>()
		);
		await _budgetProgressWriteRepository.DidNotReceive().ChangeCategoryAsync(
			userId: Arg.Any<Guid>(),
			oldCategoryId: Arg.Any<Guid>(),
			newCategoryId: Arg.Any<Guid>(),
			currencyCode: Arg.Any<FinanceTracker.Core.ValueObjects.Currency>(),
			amount: Arg.Any<decimal>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenNewCategoryDirectionMismatch_ShouldReturnInvalidTransactionDirectionException()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);
		SetupNewCategory(type: CategoryType.Income);

		FinanceTracker.Core.Results.Result<Guid, FinanceTracker.Core.Exceptions.AppException> result = await _handler.HandleAsync(
			command: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.CreateVersion7()),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<FinanceTracker.Core.Exceptions.DomainExceptions.InvalidTransactionDirectionException>();
	}
}
