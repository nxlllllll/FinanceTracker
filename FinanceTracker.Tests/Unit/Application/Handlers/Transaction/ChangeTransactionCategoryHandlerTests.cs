using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionCategoryHandlerTests
{
    private ITransactionReadRepository _transactionReadRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private ChangeTransactionCategoryHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionReadRepository = Substitute.For<ITransactionReadRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _handler = new ChangeTransactionCategoryHandler(
            transactionReadRepository: _transactionReadRepository,
            transactionWriteRepository: _transactionWriteRepository
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldChangeCategory()
    {
        Guid transactionId = Guid.NewGuid();
        Guid newCategoryId = Guid.NewGuid();

        _transactionReadRepository.ExistsAsync(
            transactionId: transactionId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: true);

        ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
            TransactionId: transactionId,
            CategoryId: newCategoryId
        );

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
            transactionId: transactionId,
            categoryId: newCategoryId,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldThrowNotFoundException()
    {
        _transactionReadRepository.ExistsAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: false);

        ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid()
        );

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldNotCallWriteRepository()
    {
        _transactionReadRepository.ExistsAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: false);

        ChangeTransactionCategoryCommand command = new ChangeTransactionCategoryCommand(
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid()
        );

        await Assert.That(action: async () =>
            await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();

        await _transactionWriteRepository.DidNotReceive().ChangeCategoryAsync(
            transactionId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}