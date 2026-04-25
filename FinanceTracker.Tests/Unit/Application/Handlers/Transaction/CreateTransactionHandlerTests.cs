using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
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
    private IUserRepository _userRepository = null!;
    private CreateTransactionHandler _handler = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
 
    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _currencyConversionService = Substitute.For<ICurrencyConversionService>();
        _userRepository = Substitute.For<IUserRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
        
        _handler = new CreateTransactionHandler(
            accountRepository: _accountRepository,
            transactionWriteRepository: _transactionWriteRepository,
            currencyConversionService: _currencyConversionService,
            userRepository: _userRepository,
            unitOfWork: _unitOfWork,
            categoryTotalWriteRepository: _categoryTotalWriteRepository
        );
    }
 
    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnTransactionId()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);
 
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));
 
        Guid result = await _handler.Handle(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId),
            ct: CancellationToken.None
        );
 
        await Assert.That(value: result).IsNotDefault();
    }
 
    [Test]
    public async Task Handle_WithDebitDirection_ShouldDecreaseAccountBalance()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(balance: 10000);
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);
 
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));
 
        await _handler.Handle(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Debit),
            ct: CancellationToken.None
        );
 
        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Balance == 9000m),
            ct: Arg.Any<CancellationToken>()
        );
    }
 
    [Test]
    public async Task Handle_WithCreditDirection_ShouldIncreaseAccountBalance()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(balance: 10000);
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);
 
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));
 
        await _handler.Handle(
            command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
            ct: CancellationToken.None
        );
 
        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Balance == 11000m),
            ct: Arg.Any<CancellationToken>()
        );
    }
 
    [Test]
    public async Task Handle_WithPendingRate_ShouldCreateTransactionWithIsPendingTrue()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);
 
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 85m, IsPending: true));
 
        await _handler.Handle(command: CreateTransactionCommandFactory.Create(userId: account.UserId), ct: CancellationToken.None);
 
        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            transactionId: Arg.Any<Guid>(),
            accountId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            amount: Arg.Any<decimal>(),
            direction: Arg.Any<DirectionType>(),
            exchangeRate: 85m,
            description: Arg.Any<string?>(),
            occurredAt: Arg.Any<DateTime>(),
            isRatePending: true,
            ct: Arg.Any<CancellationToken>()
        );
    }
 
    [Test]
    public async Task Handle_WhenAccountNotFound_ShouldThrowNotFoundException()
    {
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));
 
        await Assert.That(action: async () =>
            await _handler.Handle(command: CreateTransactionCommandFactory.Create(), ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }
 
    [Test]
    public async Task Handle_WhenUserNotFound_ShouldThrowNotFoundException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));
 
        await Assert.That(action: async () =>
            await _handler.Handle(command: CreateTransactionCommandFactory.Create(), ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }
 
    [Test]
    public async Task Handle_WhenAccountIsArchived_ShouldThrowArchivingException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);
 
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));
 
        await Assert.That(action: async () =>
            await _handler.Handle(command: CreateTransactionCommandFactory.Create(userId: account.UserId), ct: CancellationToken.None)
        ).Throws<ArchivingException>();
    }
 
    [Test]
    public async Task Handle_WhenRateNotFound_ShouldThrowCurrencyRateNotFoundException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(), 
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);
 
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
 
        await Assert.That(action: async () =>
            await _handler.Handle(command: CreateTransactionCommandFactory.Create(userId: account.UserId), ct: CancellationToken.None)
        ).Throws<CurrencyRateNotFoundException>();
    }
 
    [Test]
    public async Task Handle_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
 
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);
 
        await Assert.That(action: async () => await _handler.Handle(
            command: CreateTransactionCommandFactory.Create(accountId: account.Id, userId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }
    
    [Test]
    public async Task Handle_WithValidCommand_ShouldAddCategoryTotal()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        FinanceTracker.Core.Domains.User.User user = UserFactory.Create();

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        _userRepository.GetByIdAsync(
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: user);

        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

        CreateTransactionCommand command = CreateTransactionCommandFactory.Create(userId: account.UserId);

        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).AddAsync(
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: command.Amount,
            occurredAt: command.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }
}