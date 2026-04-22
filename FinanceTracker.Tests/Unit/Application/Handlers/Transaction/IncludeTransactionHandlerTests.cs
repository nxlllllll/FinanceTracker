using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class IncludeTransactionHandlerTests
{
    private ITransactionRepository _transactionRepository = null!;
    private IPublisher _publisher = null!;
    private IncludeTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _publisher = Substitute.For<IPublisher>();
        _handler = new IncludeTransactionHandler(transactionRepository: _transactionRepository, publisher: _publisher);
    }

    private static FinanceTracker.Core.Domains.Transactions.Transaction CreateExcludedTransaction()
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
        transaction.Exclude();
        transaction.ClearEvents();
        return transaction;
    }

    [Test]
    public async Task Handle_WithExcludedTransaction_ShouldIncludeAndPublish()
    {
        FinanceTracker.Core.Domains.Transactions.Transaction transaction = CreateExcludedTransaction();

        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        IncludeTransactionCommand command = new IncludeTransactionCommand(TransactionId: transaction.Id);

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _transactionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            transaction: Arg.Is<FinanceTracker.Core.Domains.Transactions.Transaction>(predicate: t => t.IsExcluded == false),
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

        IncludeTransactionCommand command = new IncludeTransactionCommand(TransactionId: Guid.NewGuid());

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenTransactionAlreadyIncluded_ShouldThrowIncludingException()
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

        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        IncludeTransactionCommand command = new IncludeTransactionCommand(TransactionId: transaction.Id);

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<IncludingException>();
    }
}