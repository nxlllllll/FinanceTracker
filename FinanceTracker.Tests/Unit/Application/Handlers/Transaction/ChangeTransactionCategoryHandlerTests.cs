using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionCategoryHandlerTests
{
	private ITransactionRepository _transactionRepository;
	private IPublisher _publisher;
	private ChangeTransactionCategoryHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionRepository = Substitute.For<ITransactionRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new ChangeTransactionCategoryHandler(transactionRepository: _transactionRepository, publisher: _publisher);
	}
	
	private static FinanceTracker.Core.Domains.Transactions.Transaction CreateTransaction()
    {
        FinanceTracker.Core.Domains.Transactions.Transaction transaction = FinanceTracker.Core.Domains.Transactions.Transaction.Create(
            accountId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: null,
            occurredAt: DateTime.UtcNow
        );
        transaction.ClearEvents();
        return transaction;
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldChangeCategoryAndPublish()
    {
        FinanceTracker.Core.Domains.Transactions.Transaction transaction = CreateTransaction();
        Guid newCategoryId = Guid.NewGuid();

        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
            TransactionId: transaction.Id,
            CategoryId: newCategoryId
        );

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _transactionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            transaction: Arg.Is<FinanceTracker.Core.Domains.Transactions.Transaction>(predicate: t => t.CategoryId == newCategoryId),
            ct: Arg.Any<CancellationToken>()
        );

        await _publisher.Received(requiredNumberOfCalls: 1).Publish(
            notification: Arg.Is<TransactionEventsNotification>(predicate: n => n.Events.Count == 1),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldThrowNotFoundException()
    {
        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Transactions.Transaction?>(result: null));

        ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid()
        );

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }
}