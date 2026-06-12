using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ExcludeTransactionHandlerTests
{
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private ExcludeTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
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
		
		_handler = new ExcludeTransactionHandler(
			transactionWriteRepository: _transactionWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ExcludeTransactionHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyExcluded_ShouldReturnExcludingException()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(isExcluded: true);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ExcludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ExcludingException>();
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
			transactionId: transaction.Id, 
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
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
	public async Task HandleAsync_WithIncludedDebit_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(direction: DirectionType.Debit, isExcluded: false);

		await _handler.HandleAsync(
			command: new ExcludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<TransactionExcludedNotification>(n =>
				n.TransactionId == transaction.Id && n.UserId == transaction.UserId),
			cancellationToken: Arg.Any<CancellationToken>()
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
			transactionId: transaction.Id, 
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _categoryTotalWriteRepository.DidNotReceive().SubtractAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			amount: Arg.Any<decimal>(),
			currency: Arg.Any<Currency>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAlreadyExcluded_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Transaction.Transaction transaction = TransactionFactory.Create(isExcluded: true);

		await _handler.HandleAsync(
			command: new ExcludeTransactionCommand(UserId: transaction.UserId, TransactionId: transaction.Id),
			transaction: transaction,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<TransactionExcludedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}