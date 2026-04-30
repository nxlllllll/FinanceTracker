using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
    private IAccountRepository _accountRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private ICurrencyConversionService _currencyConversionService = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
    private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
    private CreateTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _currencyConversionService = Substitute.For<ICurrencyConversionService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
        _budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();

        _handler = new CreateTransactionHandler(
            accountRepository: _accountRepository,
            transactionWriteRepository: _transactionWriteRepository,
            currencyConversionService: _currencyConversionService,
            unitOfWork: _unitOfWork,
            categoryTotalWriteRepository: _categoryTotalWriteRepository,
            budgetProgressWriteRepository: _budgetProgressWriteRepository
        );
    }

    private void SetupConversionRate(decimal rate = 1m, bool isPending = false)
    {
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: rate, IsPending: isPending));
    }

    [Test]
    public async Task HandleAsync_WithValidCommand_ShouldReturnTransactionId()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        SetupConversionRate();

        Guid result = await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        );

        await Assert.That(value: result).IsNotDefault();
    }

    [Test]
    public async Task HandleAsync_WithDebitDirection_ShouldDecreaseAccountBalance()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(balance: 10000m);
        SetupConversionRate();

        await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Debit),
            account: account,
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Balance.Amount == 9000m),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WithCreditDirection_ShouldIncreaseAccountBalance()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(balance: 10000m);
        SetupConversionRate();

        await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
            account: account,
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Balance.Amount == 11000m),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WithPendingRate_ShouldCreateTransactionWithIsRatePendingTrue()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        SetupConversionRate(rate: 85m, isPending: true);

        await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        );

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            transaction: Arg.Is<FinanceTracker.Core.Domains.Transaction.Transaction>(t => t.ExchangeRate == 85m && t.IsRatePending),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenArchivedAccount_ShouldThrowArchivingException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);
        SetupConversionRate();

        await Assert.That(action: async () => await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        )).Throws<ArchivingException>();
    }

    [Test]
    public async Task HandleAsync_WhenRateNotFound_ShouldThrowCurrencyRateNotFoundException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();

        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns<ConversionResult>(returnThis: _ => throw new CurrencyRateNotFoundException(
            message: "Rate not found.",
            fromCurrency: "USD",
            toCurrency: "RUB"
        ));

        await Assert.That(action: async () => await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            account: account,
            ct: CancellationToken.None
        )).Throws<CurrencyRateNotFoundException>();
    }

    [Test]
    public async Task HandleAsync_WithDebitDirection_ShouldAddCategoryTotal()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        SetupConversionRate();

        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
            userId: account.UserId,
            direction: DirectionType.Debit
        );

        await _handler.HandleAsync(command: command, account: account, ct: CancellationToken.None);

        await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: command.Amount,
            occurredAt: command.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WithDebitDirection_ShouldAddBudgetProgress()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        SetupConversionRate();

        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
            userId: account.UserId,
            direction: DirectionType.Debit
        );

        await _handler.HandleAsync(command: command, account: account, ct: CancellationToken.None);

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
    public async Task HandleAsync_WithCreditDirection_ShouldNotAddCategoryTotal()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        SetupConversionRate();

        await _handler.HandleAsync(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
            account: account,
            ct: CancellationToken.None
        );

        await _categoryTotalWriteRepository.DidNotReceive().AddAsync(
            userId: Arg.Any<Guid>(), categoryId: Arg.Any<Guid>(),
            amount: Arg.Any<decimal>(), occurredAt: Arg.Any<DateTime>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}