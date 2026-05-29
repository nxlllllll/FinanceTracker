using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
    private ITransactionCreationService _transactionCreationService = null!;
    private ICategoryReadRepository _categoryReadRepository = null!;
    private CreateTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _transactionCreationService = Substitute.For<ITransactionCreationService>();
        _categoryReadRepository = Substitute.For<ICategoryReadRepository>();
        
        CategoryReadModel category = CategoryFactory.CreateReadModel(type: CategoryType.Expense);
        _categoryReadRepository.GetByIdAsync(
            categoryId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: category);
        
        _handler = new CreateTransactionHandler(
            transactionCreationService: _transactionCreationService,
            categoryReadRepository: _categoryReadRepository
        );
    }

    [Test]
    public async Task HandleAsync_ShouldDelegateToService()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
            userId: account.UserId,
            accountId: account.Id
        );
        Guid transactionId = Guid.CreateVersion7();

        _transactionCreationService.CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Result<Guid, DomainException>.Success(value: transactionId));

        Result<Guid, DomainException> result = await _handler.HandleAsync(
            command: command,
            account: account,
            ct: CancellationToken.None
        );

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsEqualTo(expected: transactionId);
        await _transactionCreationService.Received(requiredNumberOfCalls: 1).CreateAsync(
            command: command,
            account: account,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenServiceReturnsFailure_ShouldReturnFailure()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;
        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
            userId: account.UserId,
            accountId: account.Id
        );
        DomainException error = new InvalidAmountException(message: "Invalid amount.");

        _transactionCreationService.CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Result<Guid, DomainException>.Failure(error: error));

        Result<Guid, DomainException> result = await _handler.HandleAsync(
            command: command,
            account: account,
            ct: CancellationToken.None
        );

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsEqualTo(expected: error);
    }
}
