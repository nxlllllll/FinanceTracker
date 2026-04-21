using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
	private ITransactionRepository _transactionRepository;
	private IPublisher _publisher;
	private CreateTransactionHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionRepository = Substitute.For<ITransactionRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new CreateTransactionHandler(transactionRepository: _transactionRepository, publisher: _publisher);
	}
	
	[Test]
    public async Task Handle_WithValidCommand_ShouldSaveTransaction()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: "Обед",
            OccurredAt: DateTime.UtcNow
        );

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _transactionRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            transaction: Arg.Is<Core.Domains.Transactions.Transaction>(t =>
                t.Amount == 1000m &&
                t.Direction == DirectionType.Debit &&
                t.IsExcluded == false
            ),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldPublishNotification()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _publisher.Received(requiredNumberOfCalls: 1).Publish(
            notification: Arg.Is<TransactionEventsNotification>(predicate: n => n.Events.Count == 1),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnTransactionId()
    {
        CreateTransactionCommand command = new CreateTransactionCommand(
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        Guid result = await _handler.Handle(command: command, ct: CancellationToken.None);

        await Assert.That(value: result).IsNotDefault();
    }
}