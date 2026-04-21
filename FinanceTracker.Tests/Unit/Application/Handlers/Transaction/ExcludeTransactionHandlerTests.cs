using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ExcludeTransactionHandlerTests
{
    private ITransactionRepository _transactionRepository;
    private IPublisher _publisher;
    private ExcludeTransactionHandler _handler;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionRepository = Substitute.For<ITransactionRepository>();
        _publisher = Substitute.For<IPublisher>();
        _handler = new ExcludeTransactionHandler(transactionRepository: _transactionRepository, publisher: _publisher);
    }

    private static Core.Domains.Transactions.Transaction CreateTransaction()
    {
        Core.Domains.Transactions.Transaction transaction = Core.Domains.Transactions.Transaction.Create(
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
    public async Task Handle_WithIncludedTransaction_ShouldExcludeAndPublish()
    {
        Core.Domains.Transactions.Transaction transaction = CreateTransaction();

        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        ExcludeTransactionCommand command = new ExcludeTransactionCommand(TransactionId: transaction.Id);

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _transactionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            transaction: Arg.Is<Core.Domains.Transactions.Transaction>(predicate: t => t.IsExcluded == true),
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
        ).Returns(returnThis: Task.FromResult<Core.Domains.Transactions.Transaction?>(result: null));

        ExcludeTransactionCommand command = new ExcludeTransactionCommand(TransactionId: Guid.NewGuid());

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenTransactionAlreadyExcluded_ShouldThrowExcludingException()
    {
        Core.Domains.Transactions.Transaction transaction = CreateTransaction();
        transaction.Exclude();
        transaction.ClearEvents();

        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        ExcludeTransactionCommand command = new ExcludeTransactionCommand(TransactionId: transaction.Id);

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<ExcludingException>();
    }	
}