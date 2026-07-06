using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Transfer;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.User;

public sealed class UserReadRepositoryTests : DatabaseFixture
{
	private UserReadRepository _readRepository = null!;
	private UserWriteRepository _writeRepository = null!;
	private CategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private TransactionWriteRepository _transactionWriteRepository = null!;
	private TransferWriteRepository _transferWriteRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private CurrencyBuilder _currencyBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		ICurrencyConversionService currencyConversionService = new CurrencyConversionService(
			currencyRateReadRepository: new CurrencyRateReadRepository(context: Context),
			logger: Substitute.For<ILogger<CurrencyConversionService>>()
		);

		_readRepository = new UserReadRepository(context: Context);
		_writeRepository = new UserWriteRepository(context: Context);
		_categoryTotalWriteRepository = new CategoryTotalWriteRepository(
			context: Context,
			userQueryRepository: _readRepository,
			currencyConversionService: currencyConversionService,
			dateProvider: FakeDateProvider.Default
		);
		_transactionWriteRepository = new TransactionWriteRepository(context: Context, operationRepository: new OperationWriteRepository(context: Context));
		_transferWriteRepository = new TransferWriteRepository(context: Context, operationRepository: new OperationWriteRepository(context: Context));
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);
	}

	private async Task<Core.Domains.User.User> CreateAndSaveUserAsync(string currencyCode = "RUB")
	{
		await _currencyBuilder.CreateAsync(code: currencyCode);
		Result<Core.Domains.User.User, DomainException> result = Core.Domains.User.User.Register(
			createdAt: FakeDateProvider.Default.UtcNow,
			email: Email.Create(value: $"{Guid.CreateVersion7()}@test.com").Value,
			passwordHash: "hash",
			baseCurrency: Core.ValueObjects.Currency.Create(value: currencyCode).Value
		);
		Core.Domains.User.User user = result.Value!;
		await _writeRepository.CreateAsync(user: user);
		await Context.SaveChangesAsync();
		return user;
	}

	private async Task SeedRateAsync(string baseCode, string targetCode, decimal rate, DateOnly date)
	{
		await _currencyBuilder.CreateAsync(code: baseCode);
		await _currencyBuilder.CreateAsync(code: targetCode);
		await Context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity
		{
			BaseCode = Core.ValueObjects.Currency.Create(value: baseCode).Value,
			TargetCode = Core.ValueObjects.Currency.Create(value: targetCode).Value,
			Rate = rate,
			ActualAt = date,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();
	}

	[Test]
	public async Task GetByIdAsync_WithNonExistentUser_ShouldReturnNull()
	{
		UserReadModel? result = await (_readRepository as IUserQueryRepository).GetByIdAsync(userId: Guid.CreateVersion7());
		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WithExistingUser_ShouldReturnCorrectUser()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync();

		UserReadModel? result = await (_readRepository as IUserQueryRepository).GetByIdAsync(userId: user.Id);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: user.Id);
		await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
		await Assert.That(value: result.BaseCurrency.Value).IsEqualTo(expected: "RUB");
	}

	[Test]
	public async Task GetByEmailAsync_WithNonExistentEmail_ShouldReturnNull()
	{
		Core.Domains.User.User? result = await _readRepository.GetByEmailAsync(email: "notexist@test.com");
		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByEmailAsync_WithExistingEmail_ShouldReturnCorrectUser()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync();

		Core.Domains.User.User? result = await _readRepository.GetByEmailAsync(email: user.Email.Value);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: user.Id);
		await Assert.That(value: result.Email).IsEqualTo(expected: user.Email);
	}

	[Test]
	public async Task GetIncomeExpenseSummaryAsync_WhenNoData_ShouldReturnZeros()
	{
		Guid userId = await _userBuilder.CreateAsync();

		(decimal income, decimal expense) = await _readRepository.GetIncomeExpenseSummaryAsync(
			userId: userId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: income).IsEqualTo(expected: 0m);
		await Assert.That(value: expense).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task GetIncomeExpenseSummaryAsync_ShouldSumByType()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid incomeCategory = await _categoryBuilder.CreateAsync(
			userId: userId,
			name: "Зарплата",
			type: CategoryType.Income
		);
		Guid expenseCategory = await _categoryBuilder.CreateAsync(
			userId: userId,
			name: "Еда",
			type: CategoryType.Expense
		);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _categoryTotalWriteRepository.AddAsync(
			userId: userId,
			categoryId: incomeCategory,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 10000m, occurredAt: occurredAt
		);
		await _categoryTotalWriteRepository.AddAsync(
			userId: userId,
			categoryId: expenseCategory,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 3000m, occurredAt: occurredAt
		);

		(decimal income, decimal expense) = await _readRepository.GetIncomeExpenseSummaryAsync(
			userId: userId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: income).IsEqualTo(expected: 10000m);
		await Assert.That(value: expense).IsEqualTo(expected: 3000m);
	}

	[Test]
	public async Task GetIncomeExpenseSummaryAsync_ShouldExcludeArchivedCategories()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid expenseCategory = await _categoryBuilder.CreateAsync(
			userId: userId,
			name: "Еда",
			type: CategoryType.Expense
		);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _categoryTotalWriteRepository.AddAsync(
			userId: userId,
			categoryId: expenseCategory,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 3000m, occurredAt: occurredAt
		);

		await Context.Categories
			.Where(c => c.Id == expenseCategory)
			.ExecuteUpdateAsync(s => s.SetProperty(c => c.IsArchived, true));

		(_, decimal expense) = await _readRepository.GetIncomeExpenseSummaryAsync(
			userId: userId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: expense).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task GetIncomeExpenseSummaryAsync_ShouldNotMixPeriods()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid expenseCategory = await _categoryBuilder.CreateAsync(
			userId: userId, name: "Еда", type: CategoryType.Expense
		);

		await _categoryTotalWriteRepository.AddAsync(
			userId: userId, categoryId: expenseCategory,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value, amount: 1000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _categoryTotalWriteRepository.AddAsync(
			userId: userId, categoryId: expenseCategory,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value, amount: 2000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 2, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		(_, decimal expenseJan) = await _readRepository.GetIncomeExpenseSummaryAsync(
			userId: userId,
			period: new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: expenseJan).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task GetHistoryAsync_WhenNoOperations_ShouldReturnEmpty()
	{
		Guid userId = await _userBuilder.CreateAsync();

		PagedResult<Operation> result = await _readRepository.GetHistoryAsync(userId: userId);

		await Assert.That(value: result.Items).IsEmpty();
		await Assert.That(value: result.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetHistoryAsync_ShouldReturnTransactionWithCorrectType()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(), accountId: accountId, userId: userId,
			categoryId: categoryId,
			amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			direction: DirectionType.Credit, exchangeRate: 1m,
			isExcluded: false, description: null, isRatePending: false,
			rowVersion: 0,
			occurredAt: FakeDateProvider.Default.UtcNow
		);
		await _transactionWriteRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		PagedResult<Operation> result = await _readRepository.GetHistoryAsync(userId: userId);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items[0].Type).IsEqualTo(expected: OperationFilterType.Income);
		await Assert.That(value: result.Items[0].Transaction).IsNotNull();
		await Assert.That(value: result.Items[0].Transfer).IsNull();
	}

	[Test]
	public async Task GetHistoryAsync_ShouldReturnTransferWithCorrectType()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid fromAccountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid toAccountId = await _accountBuilder.CreateAsync(userId: userId);

		Core.Domains.Transfer.Transfer transfer = Core.Domains.Transfer.Transfer.Create(
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amount: 1000m,
			currencyFrom: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			currencyTo: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			exchangeRate: 1m,
			isRatePending: false,
			description: null,
			occurredAt: FakeDateProvider.Default.UtcNow
		).Value!;
		await _transferWriteRepository.CreateAsync(transfer: transfer);
		await Context.SaveChangesAsync();

		PagedResult<Operation> result = await _readRepository.GetHistoryAsync(userId: userId);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items[0].Type).IsEqualTo(expected: OperationFilterType.Transfer);
		await Assert.That(value: result.Items[0].Transfer).IsNotNull();
		await Assert.That(value: result.Items[0].Transaction).IsNull();
	}

	[Test]
	public async Task GetHistoryAsync_WhenFilterByIncome_ShouldReturnOnlyIncomeTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.Transaction.Transaction income = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(), accountId: accountId, userId: userId, categoryId: categoryId,
			amount: Money.Create(amount: 5000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			direction: DirectionType.Credit, exchangeRate: 1m,
			isExcluded: false, description: null, isRatePending: false,
			rowVersion: 0,
			occurredAt: FakeDateProvider.Default.UtcNow
		);
		Core.Domains.Transaction.Transaction expense = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(), accountId: accountId, userId: userId, categoryId: categoryId,
			amount: Money.Create(amount: 500m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			direction: DirectionType.Debit, exchangeRate: 1m,
			isExcluded: false, description: null, isRatePending: false,
			rowVersion: 0,
			occurredAt: FakeDateProvider.Default.UtcNow
		);
		await _transactionWriteRepository.CreateAsync(transaction: income);
		await _transactionWriteRepository.CreateAsync(transaction: expense);
		await Context.SaveChangesAsync();

		PagedResult<Operation> result = await _readRepository.GetHistoryAsync(
			userId: userId,
			type: OperationFilterType.Income
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items[0].Type).IsEqualTo(expected: OperationFilterType.Income);
	}

	[Test]
	public async Task GetHistoryAsync_ShouldRespectPageSize()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		for (int i = 0; i < 5; i++)
		{
			Core.Domains.Transaction.Transaction tx = Core.Domains.Transaction.Transaction.Reconstitute(
				id: Guid.CreateVersion7(), accountId: accountId, userId: userId, categoryId: categoryId,
				amount: Money.Create(amount: 100m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
				baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
				direction: DirectionType.Debit, exchangeRate: 1m,
				isExcluded: false, description: null, isRatePending: false,
				rowVersion: 0,
				occurredAt: FakeDateProvider.Default.UtcNow.AddSeconds(i)
			);
			await _transactionWriteRepository.CreateAsync(transaction: tx);
		}
		await Context.SaveChangesAsync();

		PagedResult<Operation> result = await _readRepository.GetHistoryAsync(
			userId: userId,
			pageSize: 3
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 3);
		await Assert.That(value: result.HasNextPage).IsTrue();
		await Assert.That(value: result.NextCursorDate).IsNotNull();
		await Assert.That(value: result.NextCursorId).IsNotNull();
	}

	[Test]
	public async Task GetHistoryAsync_ShouldOrderByOccurredAtDescending()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset earlier = FakeDateProvider.Default.UtcNow.AddHours(hours: -1);
		DateTimeOffset later = FakeDateProvider.Default.UtcNow;

		Core.Domains.Transaction.Transaction first = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(), accountId: accountId, userId: userId, categoryId: categoryId,
			amount: Money.Create(amount: 100m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			direction: DirectionType.Debit, exchangeRate: 1m,
			isExcluded: false, description: "Earlier", isRatePending: false,
			rowVersion: 0,
			occurredAt: earlier
		);
		Core.Domains.Transaction.Transaction second = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(), accountId: accountId, userId: userId, categoryId: categoryId,
			amount: Money.Create(amount: 200m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			direction: DirectionType.Debit, exchangeRate: 1m,
			isExcluded: false, description: "Later", isRatePending: false,
			rowVersion: 0,
			occurredAt: later
		);
		await _transactionWriteRepository.CreateAsync(transaction: first);
		await _transactionWriteRepository.CreateAsync(transaction: second);
		await Context.SaveChangesAsync();

		PagedResult<Operation> result = await _readRepository.GetHistoryAsync(userId: userId);

		await Assert.That(value: result.Items[0].Description).IsEqualTo(expected: "Later");
		await Assert.That(value: result.Items[1].Description).IsEqualTo(expected: "Earlier");
	}

	[Test]
	public async Task GetTotalBalanceAsync_WhenNoAccounts_ShouldReturnZero()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync();

		decimal result = await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime)
		);

		await Assert.That(value: result).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task GetTotalBalanceAsync_WithSingleAccountInBaseCurrency_ShouldReturnBalance()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync();
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "RUB", balance: 5000m);

		decimal result = await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime)
		);

		await Assert.That(value: result).IsEqualTo(expected: 5000m);
	}

	[Test]
	public async Task GetTotalBalanceAsync_WithMultipleAccounts_ShouldSumBalances()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync();
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "RUB", balance: 3000m);
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "RUB", balance: 7000m);

		decimal result = await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime)
		);

		await Assert.That(value: result).IsEqualTo(expected: 10000m);
	}

	[Test]
	public async Task GetTotalBalanceAsync_WithForeignCurrencyAccount_ShouldConvertUsingRate()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync(currencyCode: "RUB");
		DateOnly today = DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime);
		await SeedRateAsync(baseCode: "USD", targetCode: "RUB", rate: 90m, date: today);
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "USD", balance: 100m);

		decimal result = await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: today
		);

		await Assert.That(value: result).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task GetTotalBalanceAsync_ShouldExcludeArchivedAccounts()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync();
		_ = await _accountBuilder.CreateAsync(
			userId: user.Id, currencyCode: "RUB", balance: 5000m
		);
		Guid archivedAccountId = await _accountBuilder.CreateAsync(
			userId: user.Id, currencyCode: "RUB", balance: 3000m
		);

		await Context.Accounts
			.Where(a => a.Id == archivedAccountId)
			.ExecuteUpdateAsync(s => s.SetProperty(a => a.IsArchived, true));

		decimal result = await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime)
		);

		await Assert.That(value: result).IsEqualTo(expected: 5000m);
	}

	[Test]
	public async Task GetTotalBalanceAsync_WithForeignCurrencyAccountAndNoRateAtAll_ShouldThrowCurrencyRateMissingException()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync(currencyCode: "RUB");
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "USD", balance: 100m);

		CurrencyRateMissingException? exception = await Assert.ThrowsAsync<CurrencyRateMissingException>(action: async () => await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime)
		));

		await Assert.That(value: exception).IsNotNull();
		await Assert.That(value: exception!.FromCurrency).IsEqualTo(expected: Core.ValueObjects.Currency.Reconstitute(value: "USD"));
		await Assert.That(value: exception.ToCurrency).IsEqualTo(expected: user.BaseCurrency);
	}

	[Test]
	public async Task GetTotalBalanceAsync_WithNoExactRateButHistoricalRateExists_ShouldFallBackWithoutThrowing()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync(currencyCode: "RUB");
		DateOnly today = DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime);
		DateOnly lastWeek = today.AddDays(value: -7);
		await SeedRateAsync(baseCode: "USD", targetCode: "RUB", rate: 85m, date: lastWeek);
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "USD", balance: 100m);

		decimal result = await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: today
		);

		await Assert.That(value: result).IsEqualTo(expected: 8500m);
	}

	[Test]
	public async Task GetTotalBalanceAsync_WithOneAccountMissingRateAmongMultiple_ShouldThrowBeforeSummingAny()
	{
		Core.Domains.User.User user = await CreateAndSaveUserAsync(currencyCode: "RUB");
		DateOnly today = DateOnly.FromDateTime(dateTime: FakeDateProvider.Default.UtcNow.UtcDateTime);
		await SeedRateAsync(baseCode: "USD", targetCode: "RUB", rate: 90m, date: today);
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "RUB", balance: 5000m);
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "USD", balance: 100m);
		await _accountBuilder.CreateAsync(userId: user.Id, currencyCode: "EUR", balance: 50m);

		await Assert.ThrowsAsync<CurrencyRateMissingException>(action: async () => await _readRepository.GetTotalBalanceAsync(
			userId: user.Id,
			baseCurrency: user.BaseCurrency,
			date: today
		));
	}
}
