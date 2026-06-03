using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Services;

public sealed class TransactionCreationServiceTests
{
    private IAccountRepository _accountRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private ICurrencyConversionService _currencyConversionService = null!;
    private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
    private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private TransactionCreationService _service = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _currencyConversionService = Substitute.For<ICurrencyConversionService>();
        _categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
        _budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
        _service = new TransactionCreationService(
            accountRepository: _accountRepository,
            transactionWriteRepository: _transactionWriteRepository,
            currencyConversionService: _currencyConversionService,
            unitOfWork: _unitOfWork,
            categoryTotalWriteRepository: _categoryTotalWriteRepository,
            budgetProgressWriteRepository: _budgetProgressWriteRepository,
            logger: Substitute.For<ILogger<TransactionCreationService>>()
        );
    }

    private void SetupConversionRate(decimal rate = 1m, bool isPending = false)
    {
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<Currency>(),
            toCurrency: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: rate, IsPending: isPending));
    }

    [Test]
    public async Task CreateAsync_WithValidCommand_ShouldReturnTransactionId()
    {
        Account account = AccountFactory.CreateWithArchivation();
        SetupConversionRate();

        Result<Guid, DomainException> result = await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        );

        await Assert.That(value: result.IsSuccess).IsTrue();
        await Assert.That(value: result.Value).IsNotDefault();
    }

    [Test]
    public async Task CreateAsync_WithDebitDirection_ShouldDecreaseAccountBalance()
    {
        Account account = AccountFactory.CreateWithArchivation(balance: 10000m);
        SetupConversionRate();

        await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Debit),
            account: account,
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<Account>(predicate: a => a.Balance.Amount == 9000m),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task CreateAsync_WithCreditDirection_ShouldIncreaseAccountBalance()
    {
        Account account = AccountFactory.CreateWithArchivation(balance: 10000m);
        SetupConversionRate();

        await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
            account: account,
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<Account>(predicate: a => a.Balance.Amount == 11000m),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task CreateAsync_WithPendingRate_ShouldCreateTransactionWithIsRatePendingTrue()
    {
        Account account = AccountFactory.CreateWithArchivation();
        SetupConversionRate(rate: 0.85m, isPending: true);

        await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        );

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            transaction: Arg.Is<Transaction>(predicate: t => t.ExchangeRate == 0.85m && t.IsRatePending),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task CreateAsync_WhenArchivedAccount_ShouldThrowArchivingException()
    {
        Account account = AccountFactory.CreateWithArchivation(archived: true);
        SetupConversionRate();

        Result<Guid, DomainException> result = await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        );
        
        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<ArchivedAccountOperationException>();
    }

    [Test]
    public async Task CreateAsync_WhenRateNotFound_ShouldThrowCurrencyRateNotFoundException()
    {
        Account account = AccountFactory.CreateWithArchivation();

        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<Currency>(),
            toCurrency: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns<ConversionResult>(returnThis: _ => throw new CurrencyRateNotFoundException(
            message: "Rate not found.",
            fromCurrency: Currency.Reconstitute(value: "USD"),
            toCurrency: Currency.Reconstitute(value: "RUB")
        ));

        await Assert.That(action: async () => await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        )).Throws<CurrencyRateNotFoundException>();
    }

    [Test]
    public async Task CreateAsync_WithDebitDirection_ShouldAddCategoryTotal()
    {
        Account account = AccountFactory.CreateWithArchivation();
        SetupConversionRate();

        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
            userId: account.UserId,
            direction: DirectionType.Debit
        );

        await _service.CreateAsync(command: command, account: account, ct: CancellationToken.None);

        await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: command.Amount,
            currency: command.Currency,
            occurredAt: command.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task CreateAsync_WithDebitDirection_ShouldAddBudgetProgress()
    {
        Account account = AccountFactory.CreateWithArchivation();
        SetupConversionRate();

        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
            userId: account.UserId,
            direction: DirectionType.Debit
        );

        await _service.CreateAsync(command: command, account: account, ct: CancellationToken.None);

        await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
            userId: command.UserId,
            categoryId: command.CategoryId,
            currencyCode: command.Currency,
            amount: command.Amount,
            occurredAt: command.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task CreateAsync_WithCreditDirection_ShouldNotAddCategoryTotal()
    {
        Account account = AccountFactory.CreateWithArchivation();
        SetupConversionRate();

        await _service.CreateAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
            account: account,
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.DidNotReceive().AddAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            amount: Arg.Any<decimal>(),
            currency: Arg.Any<Currency>(),
            occurredAt: Arg.Any<DateTimeOffset>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}
