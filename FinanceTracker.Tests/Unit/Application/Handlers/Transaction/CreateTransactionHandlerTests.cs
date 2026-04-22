using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
	private ITransactionRepository _transactionRepository = null!;
	private CreateTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionRepository = Substitute.For<ITransactionRepository>();
		_handler = new CreateTransactionHandler(transactionRepository: _transactionRepository);
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
            transaction: Arg.Is<FinanceTracker.Core.Domains.Transaction.Transaction>(t =>
                t.Amount == 1000m &&
                t.Direction == DirectionType.Debit &&
                t.IsExcluded == false
            ),
            ct: Arg.Any<CancellationToken>()
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