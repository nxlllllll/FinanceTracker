using FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ExcludeTransactionHandlerTests
{
    private ITransactionReadRepository _transactionReadRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private ExcludeTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionReadRepository = Substitute.For<ITransactionReadRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _handler = new ExcludeTransactionHandler(
            transactionReadRepository: _transactionReadRepository,
            transactionWriteRepository: _transactionWriteRepository
        );
    }

    [Test]
    public async Task Handle_WithExistingTransaction_ShouldExclude()
    {
        Guid transactionId = Guid.NewGuid();

        _transactionReadRepository.ExistsAsync(
            transactionId: transactionId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: true);

        await _handler.Handle(
            command: new ExcludeTransactionCommand(TransactionId: transactionId),
            ct: CancellationToken.None
        );

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ExcludeAsync(
            transactionId: transactionId,
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

        await Assert.That(action: async () => await _handler.Handle(
            command: new ExcludeTransactionCommand(TransactionId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldNotCallWriteRepository()
    {
        _transactionReadRepository.ExistsAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: false);

        await Assert.That(action: async () => await _handler.Handle(
            command: new ExcludeTransactionCommand(TransactionId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();

        await _transactionWriteRepository.DidNotReceive().ExcludeAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}