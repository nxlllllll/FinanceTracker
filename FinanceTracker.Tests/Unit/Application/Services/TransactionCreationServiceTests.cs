using FinanceTracker.Application.Services.Transactions;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Category;
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
	private ICategoryReadRepository _categoryReadRepository = null!;
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
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_currencyConversionService = Substitute.For<ICurrencyConversionService>();
		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		SetupCategory(type: CategoryType.Expense);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());

		_service = new TransactionCreationService(
			accountRepository: _accountRepository,
			categoryReadRepository: _categoryReadRepository,
			transactionWriteRepository: _transactionWriteRepository,
			currencyConversionService: _currencyConversionService,
			unitOfWork: _unitOfWork,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<TransactionCreationService>>()
		);
	}

	private void SetupConversionRate(decimal rate = 1m, RateStatus status = RateStatus.Exact)
	{
		_currencyConversionService.GetConversionRateAsync(
			fromCurrency: Arg.Any<Currency>(),
			toCurrency: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new ConversionResult(Rate: rate, Status: status));
	}

	private CategoryReadModel SetupCategory(
		CategoryType type = CategoryType.Expense,
		bool archived = false,
		Guid? categoryId = null,
		Guid? userId = null)
	{
		CategoryReadModel category = CategoryFactory.CreateReadModel(
			userId: userId,
			type: type,
			archived: archived
		) with
		{ Id = categoryId ?? Guid.CreateVersion7() };

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		return category;
	}

	[Test]
	public async Task CreateAsync_WithValidCommand_ShouldReturnCreatedTransaction()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();

		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(userId: account.UserId);

		Result<Transaction, DomainException> result = await _service.CreateAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotNull();
		await Assert.That(value: result.Value!.UserId).IsEqualTo(expected: command.UserId);
		await Assert.That(value: result.Value!.AccountId).IsEqualTo(expected: command.AccountId);
		await Assert.That(value: result.Value!.CategoryId).IsEqualTo(expected: command.CategoryId);
	}

	[Test]
	public async Task CreateAsync_ShouldReturnTransactionWithCommandOccurredAt()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();

		DateTimeOffset expectedOccurredAt = new DateTimeOffset(
			year: 2025, month: 3, day: 15,
			hour: 10, minute: 0, second: 0,
			offset: TimeSpan.Zero
		);
		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			occurredAt: expectedOccurredAt
		);

		Result<Transaction, DomainException> result = await _service.CreateAsync(command: command, account: account, ct: CancellationToken.None);

		await Assert.That(value: result.Value!.OccurredAt).IsEqualTo(expected: expectedOccurredAt);
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
			account: Arg.Is<Account>(predicate: a => a!.Balance.Amount == 9000m),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CreateAsync_WithCreditDirection_ShouldIncreaseAccountBalance()
	{
		Account account = AccountFactory.CreateWithArchivation(balance: 10000m);
		SetupConversionRate();
		SetupCategory(type: CategoryType.Income);

		await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<Account>(predicate: a => a!.Balance.Amount == 11000m),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CreateAsync_WithPendingRate_ShouldCreateTransactionWithRateStatusPending()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate(rate: 0.85m, status: RateStatus.Pending);

		await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId),
			account: account,
			ct: CancellationToken.None
		);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			transaction: Arg.Is<Transaction>(predicate: t => t!.ExchangeRate == 0.85m && t.RateStatus == RateStatus.Pending),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task CreateAsync_WhenArchivedAccount_ShouldReturnArchivedOperationException()
	{
		Account account = AccountFactory.CreateWithArchivation(archived: true);
		SetupConversionRate();

		Result<Transaction, DomainException> result = await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
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
		).Returns<ConversionResult>(returnThis: _ => throw new CurrencyRateMissingException(
			message: "Rate not found.",
			fromCurrency: Currency.Reconstitute(value: "USD"),
			toCurrency: Currency.Reconstitute(value: "RUB")
		));

		await Assert.That(action: async () => await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId),
			account: account,
			ct: CancellationToken.None
		)).Throws<CurrencyRateMissingException>();
	}

	[Test]
	public async Task CreateAsync_WhenCategoryNotFound_ShouldReturnNotFoundException()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (CategoryReadModel?)null);

		Result<Transaction, DomainException> result = await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task CreateAsync_WhenCategoryArchived_ShouldReturnArchivedOperationException()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();
		SetupCategory(type: CategoryType.Expense, archived: true);

		Result<Transaction, DomainException> result = await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Debit),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task CreateAsync_WhenDebitDirectionWithIncomeCategory_ShouldReturnInvalidTransactionDirectionException()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();
		SetupCategory(type: CategoryType.Income);

		Result<Transaction, DomainException> result = await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Debit),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransactionDirectionException>();
	}

	[Test]
	public async Task CreateAsync_WhenCreditDirectionWithExpenseCategory_ShouldReturnInvalidTransactionDirectionException()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();
		SetupCategory(type: CategoryType.Expense);

		Result<Transaction, DomainException> result = await _service.CreateAsync(
			command: CreateTransactionCommandFactory.Create(userId: account.UserId, direction: DirectionType.Credit),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransactionDirectionException>();
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
	public async Task CreateAsync_WithCreditDirection_ShouldAddCategoryTotal()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();

		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			direction: DirectionType.Credit
		);
		SetupCategory(type: CategoryType.Income, categoryId: command.CategoryId, userId: account.UserId);

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
	public async Task CreateAsync_WithCreditDirection_ShouldNotAddBudgetProgress()
	{
		Account account = AccountFactory.CreateWithArchivation();
		SetupConversionRate();

		CreateTransactionCommand command = CreateTransactionCommandFactory.Create(
			userId: account.UserId,
			direction: DirectionType.Credit
		);
		SetupCategory(type: CategoryType.Income, categoryId: command.CategoryId, userId: account.UserId);

		await _service.CreateAsync(command: command, account: account, ct: CancellationToken.None);

		await _budgetProgressWriteRepository.DidNotReceive().AddAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			currencyCode: Arg.Any<Currency>(),
			amount: Arg.Any<decimal>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
