using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionCategoryHandlerTests
{
	private ITransactionRepository _transactionRepository = null!;
	private ChangeTransactionCategoryHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionRepository = Substitute.For<ITransactionRepository>();
		_handler = new ChangeTransactionCategoryHandler(transactionRepository: _transactionRepository);
	}
	
	private static FinanceTracker.Core.Domains.Transaction.Transaction CreateTransaction()
    {
        FinanceTracker.Core.Domains.Transaction.Transaction transaction = FinanceTracker.Core.Domains.Transaction.Transaction.Create(
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
    public async Task Handle_WithValidCommand_ShouldChangeCategory()
    {
        FinanceTracker.Core.Domains.Transaction.Transaction transaction = CreateTransaction();
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
            transaction: Arg.Is<FinanceTracker.Core.Domains.Transaction.Transaction>(predicate: t => t.CategoryId == newCategoryId),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldThrowNotFoundException()
    {
        _transactionRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Transaction.Transaction?>(result: null));

        ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid()
        );

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }
}