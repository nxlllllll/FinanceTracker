using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transfer;

public sealed class CreateTransferHandlerTests
{
    private CreateTransferHandler _handler = null!;
    private IAccountRepository _accountRepository = null!;
    private ITransferWriteRepository _transferWriteRepository = null!;
    private ICurrencyConversionService _currencyConversionService = null!;
    private IUnitOfWork _unitOfWork = null!;
    
    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transferWriteRepository = Substitute.For<ITransferWriteRepository>();
        _currencyConversionService = Substitute.For<ICurrencyConversionService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(new ConversionResult(Rate: 1m, IsPending: false));

        _handler = new CreateTransferHandler(
            accountRepository: _accountRepository,
            transferWriteRepository: _transferWriteRepository,
            currencyConversionService: _currencyConversionService,
            unitOfWork: _unitOfWork
        );
    }

    [Test]
    public async Task Handle_ShouldDebitFromAccount_AndCreditToAccount()
    {
        Guid userId = Guid.NewGuid();
        FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create(userId: userId);
        FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.Create(userId: userId);

        _accountRepository.GetByIdAsync(
            accountId: fromAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: fromAccount);
        _accountRepository.GetByIdAsync(
            accountId: toAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: toAccount);

        Guid transferId = await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: fromAccount.Id,
                ToAccountId: toAccount.Id,
                Amount: 300m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ), 
            ct: CancellationToken.None
        );

        await Assert.That(value: transferId).IsNotEqualTo(notExpected: Guid.Empty);

        await _transferWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            transferId: transferId,
            userId: userId,
            fromAccountId: fromAccount.Id,
            toAccountId: toAccount.Id,
            amountFrom: 300m,
            amountTo: 300m,
            exchangeRate: 1m,
            description: null,
            occurredAt: Arg.Any<DateTime>(),
            isRatePending: false,
            ct: Arg.Any<CancellationToken>()
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: fromAccount,
            ct: Arg.Any<CancellationToken>()
        );
        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: toAccount,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenSameAccount_ShouldThrowInvalidOperationException()
    {
        Guid userId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        await Assert.That(async () => await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: accountId,
                ToAccountId: accountId,
                Amount: 100m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ), 
            ct: CancellationToken.None
        )).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Handle_WhenFromAccountNotFound_ShouldThrowNotFoundException()
    {
        Guid userId = Guid.NewGuid();

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

        await Assert.That(async () => await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: Guid.NewGuid(),
                ToAccountId: Guid.NewGuid(),
                Amount: 100m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ), 
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenFromAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
    {
        Guid userId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create(userId: otherUserId);
        FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.Create(userId: userId);

        _accountRepository.GetByIdAsync(
            accountId: fromAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: fromAccount);
        _accountRepository.GetByIdAsync(
            accountId: toAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: toAccount);

        await Assert.That(async () => await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: fromAccount.Id,
                ToAccountId: toAccount.Id,
                Amount: 100m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ), 
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenFromAccountArchived_ShouldThrowArchivingException()
    {
        Guid userId = Guid.NewGuid();
        FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create(userId: userId);
        FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.Create(userId: userId);
        fromAccount.Archive();
        fromAccount.ClearEvents();

        _accountRepository.GetByIdAsync(
            accountId: fromAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: fromAccount);
        _accountRepository.GetByIdAsync(
            accountId: toAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: toAccount);

        await Assert.That(async () => await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: fromAccount.Id,
                ToAccountId: toAccount.Id,
                Amount: 100m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ),
            ct: CancellationToken.None
        )).Throws<ArchivingException>();
    }

    [Test]
    public async Task Handle_WhenDifferentCurrencies_ShouldApplyExchangeRate()
    {
        Guid userId = Guid.NewGuid();
        FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create(userId: userId, currency: "RUB");
        FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.Create(userId: userId, currency: "USD");

        _accountRepository.GetByIdAsync(
            accountId: fromAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: fromAccount);
        _accountRepository.GetByIdAsync(
            accountId: toAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: toAccount);

        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: "RUB",
            toCurrency: "USD",
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(new ConversionResult(Rate: 0.011m, IsPending: false));

        Guid transferId = await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: fromAccount.Id,
                ToAccountId: toAccount.Id,
                Amount: 1000m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ), 
            ct: CancellationToken.None
        );

        await _transferWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            transferId: transferId,
            userId: userId,
            fromAccountId: fromAccount.Id,
            toAccountId: toAccount.Id,
            amountFrom: 1000m,
            amountTo: 11m,
            exchangeRate: 0.011m,
            description: null,
            occurredAt: Arg.Any<DateTime>(),
            isRatePending: false,
            ct: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task Handle_WhenSecondSaveFails_ShouldRollbackTransaction()
    {
        Guid userId = Guid.NewGuid();
        FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.Create(userId: userId);
        FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.Create(userId: userId);
        
        _accountRepository.GetByIdAsync(
            accountId: fromAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: fromAccount);
        _accountRepository.GetByIdAsync(
            accountId: toAccount.Id,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: toAccount);

        _accountRepository.SaveAsync(
            account: toAccount,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: _ => throw new InvalidOperationException("DB error"));

        await Assert.That(async () => await _handler.Handle(
            command: new CreateTransferCommand(
                UserId: userId,
                FromAccountId: fromAccount.Id,
                ToAccountId: toAccount.Id,
                Amount: 100m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            ),
            ct: CancellationToken.None
        )).Throws<InvalidOperationException>();

        await _unitOfWork.Received(requiredNumberOfCalls: 1).RollbackAsync(ct: Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(ct: Arg.Any<CancellationToken>());
    }
}