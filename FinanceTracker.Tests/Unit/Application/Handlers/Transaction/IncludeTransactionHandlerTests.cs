using FinanceTracker.Application.Transactions.Commands.IncludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class IncludeTransactionHandlerTests
{
    private ITransactionReadRepository _transactionReadRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
    private IncludeTransactionHandler _handler = null!;
    private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
    private IUnitOfWork _unitOfWork = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionReadRepository = Substitute.For<ITransactionReadRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _categoryTotalWriteRepository  = Substitute.For<ICategoryTotalWriteRepository>();
        _budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _handler = new IncludeTransactionHandler(
            transactionReadRepository: _transactionReadRepository,
            transactionWriteRepository: _transactionWriteRepository,
            categoryTotalWriteRepository: _categoryTotalWriteRepository,
            budgetProgressWriteRepository: _budgetProgressWriteRepository,
            unitOfWork: _unitOfWork
        );
    }

    [Test]
    public async Task Handle_WithExistingTransaction_ShouldInclude()
    {
        TransactionDto transaction = TransactionFactory.Create(isExcluded: true);

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new IncludeTransactionCommand(
                UserId: transaction.UserId,
                TransactionId: transaction.Id
            ),
            ct: CancellationToken.None
        );

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).IncludeAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionIsExcluded_ShouldAddCategoryTotal()
    {
        TransactionDto transaction = TransactionFactory.Create(isExcluded: true);

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new IncludeTransactionCommand(
                UserId: transaction.UserId,
                TransactionId: transaction.Id
            ),
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
            userId: transaction.UserId,
            categoryId: transaction.CategoryId,
            amount: transaction.Amount,
            occurredAt: transaction.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionAlreadyIncluded_ShouldNotAddCategoryTotal()
    {
        TransactionDto transaction = TransactionFactory.Create(isExcluded: false);

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new IncludeTransactionCommand(
                UserId: transaction.UserId,
                TransactionId: transaction.Id
            ),
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.DidNotReceive().AddAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
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
            command: new IncludeTransactionCommand(
                UserId: Guid.NewGuid(),
                TransactionId: Guid.NewGuid()
            ),
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
            command: new IncludeTransactionCommand(
                UserId: Guid.NewGuid(),
                TransactionId: Guid.NewGuid()
            ),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();

        await _transactionWriteRepository.DidNotReceive().IncludeAsync(
            transactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task Handle_WhenTransactionIsCreditDirection_ShouldNotAddCategoryTotal()
    {
        TransactionDto transaction = TransactionFactory.Create(
            isExcluded: true,
            direction: DirectionType.Credit
        );

        _transactionReadRepository.GetByIdAsync(
            transactionId: transaction.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: transaction);

        await _handler.Handle(
            command: new IncludeTransactionCommand(
                UserId: transaction.UserId,
                TransactionId: transaction.Id
            ),
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.DidNotReceive().AddAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            amount: Arg.Any<decimal>(),
            occurredAt: Arg.Any<DateTime>(),
            ct: Arg.Any<CancellationToken>()
        );

        await _budgetProgressWriteRepository.DidNotReceive().AddAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            currencyCode: Arg.Any<string>(),
            amount: Arg.Any<decimal>(),
            occurredAt: Arg.Any<DateTime>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}