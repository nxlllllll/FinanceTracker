using FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class CreateRecurringTransactionHandlerTests
{
    private IRecurringTransactionWriteRepository _writeRepository = null!;
    private IAccountReadRepository _accountReadRepository = null!;
    private CreateRecurringTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
        _accountReadRepository = Substitute.For<IAccountReadRepository>();

        _handler = new CreateRecurringTransactionHandler(
            recurringTransactionWriteRepository: _writeRepository,
            accountReadRepository: _accountReadRepository
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnRecurringTransactionId()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
        _accountReadRepository.GetByIdAsync(
            accountId: command.AccountId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: AccountFactory.CreateAccountDto(id: command.AccountId, userId: command.UserId));

        Guid result = await _handler.Handle(command: command, ct: CancellationToken.None);

        await Assert.That(value: result).IsNotEqualTo(notExpected: Guid.Empty);
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldCallCreateAsync()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
        _accountReadRepository.GetByIdAsync(
            accountId: command.AccountId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: AccountFactory.CreateAccountDto(id: command.AccountId, userId: command.UserId));

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _writeRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            userId: command.UserId,
            accountId: command.AccountId,
            categoryId: command.CategoryId,
            amount: command.Amount,
            currency: command.Currency,
            direction: command.Direction,
            dayOfMonth: command.DayOfMonth,
            description: command.Description,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenAccountNotFound_ShouldThrowNotFoundException()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
        _accountReadRepository.GetByIdAsync(
            accountId: command.AccountId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (AccountDto?)null);

        await Assert.That(
            action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
    {
        CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
        _accountReadRepository.GetByIdAsync(
            accountId: command.AccountId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: AccountFactory.CreateAccountDto(id: command.AccountId, userId: Guid.NewGuid()));

        await Assert.That(
            action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }
}