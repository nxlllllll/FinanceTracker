using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class IncludeTransactionHandlerTests
{
    private ITransactionReadRepository _transactionReadRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private IncludeTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionReadRepository = Substitute.For<ITransactionReadRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _handler = new IncludeTransactionHandler(
            transactionReadRepository: _transactionReadRepository,
            transactionWriteRepository: _transactionWriteRepository
        );
    }

    [Test]
    public async Task Handle_WithExistingTransaction_ShouldInclude()
    {
        Guid transactionId = Guid.NewGuid();

        _transactionReadRepository.ExistsAsync(
            transactionId: transactionId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: true);

        await _handler.Handle(
            command: new IncludeTransactionCommand(TransactionId: transactionId),
            ct: CancellationToken.None
        );

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).IncludeAsync(
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
            command: new IncludeTransactionCommand(TransactionId: Guid.NewGuid()),
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
            command: new IncludeTransactionCommand(TransactionId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();

        await _transactionWriteRepository.DidNotReceive().IncludeAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}