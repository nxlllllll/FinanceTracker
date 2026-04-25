using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class ChangeTransactionCategoryHandlerTests
{
    private ITransactionReadRepository _transactionReadRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
    private ChangeTransactionCategoryHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionReadRepository = Substitute.For<ITransactionReadRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
        _handler = new ChangeTransactionCategoryHandler(
            transactionReadRepository: _transactionReadRepository,
            transactionWriteRepository: _transactionWriteRepository,
            categoryTotalWriteRepository: _categoryTotalWriteRepository
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldChangeCategory()
    {
        TransactionDto transaction = TransactionFactory.Create();
        Guid newCategoryId = Guid.NewGuid();

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new ChangeTransactionCategoryCommand(TransactionId: transaction.Id, CategoryId: newCategoryId),
            ct: CancellationToken.None
        );

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
            transactionId: transaction.Id,
            categoryId: newCategoryId,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionNotExcluded_ShouldMoveCategoryTotal()
    {
        TransactionDto transaction = TransactionFactory.Create(isExcluded: false);
        Guid newCategoryId = Guid.NewGuid();

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new ChangeTransactionCategoryCommand(TransactionId: transaction.Id, CategoryId: newCategoryId),
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
            userId: transaction.UserId,
            oldCategoryId: transaction.CategoryId,
            newCategoryId: newCategoryId,
            amount: transaction.Amount,
            occurredAt: transaction.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionIsExcluded_ShouldNotMoveCategoryTotal()
    {
        TransactionDto transaction = TransactionFactory.Create(isExcluded: true);
        Guid newCategoryId = Guid.NewGuid();

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new ChangeTransactionCategoryCommand(TransactionId: transaction.Id, CategoryId: newCategoryId),
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.DidNotReceive().ChangeCategoryAsync(
            userId: Arg.Any<Guid>(),
            oldCategoryId: Arg.Any<Guid>(),
            newCategoryId: Arg.Any<Guid>(),
            amount: Arg.Any<decimal>(),
            occurredAt: Arg.Any<DateTime>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldThrowNotFoundException()
    {
        _transactionReadRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<TransactionDto?>(result: null));

        await Assert.That(action: async () => await _handler.Handle(
            command: new ChangeTransactionCategoryCommand(TransactionId: Guid.NewGuid(), CategoryId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenTransactionNotFound_ShouldNotCallWriteRepository()
    {
        _transactionReadRepository.GetByIdAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<TransactionDto?>(result: null));

        await Assert.That(action: async () => await _handler.Handle(
            command: new ChangeTransactionCategoryCommand(TransactionId: Guid.NewGuid(), CategoryId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();

        await _transactionWriteRepository.DidNotReceive().ChangeCategoryAsync(
            transactionId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}