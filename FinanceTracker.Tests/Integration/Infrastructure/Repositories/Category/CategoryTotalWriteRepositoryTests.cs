using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Infrastructure.Services.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Category;

public sealed class CategoryTotalWriteRepositoryTests : DatabaseFixture
{
	private CategoryTotalWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private TransactionBuilder _transactionBuilder = null!;
	private IUserQueryRepository _userQueryRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_userQueryRepository = new UserReadRepository(context: Context);
		_currencyConversionService = new CurrencyConversionService(
			currencyRateReadRepository: new CurrencyRateReadRepository(context: Context),
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<CurrencyConversionService>>()
		);

		_writeRepository = new CategoryTotalWriteRepository(
			context: Context,
			userQueryRepository: _userQueryRepository,
			currencyConversionService: _currencyConversionService,
			options: new FakeOptionsMonitor<CategoryTotalOptions>(value: new CategoryTotalOptions
			{
				RecalculationBatchSize = 100
			}),
			dateProvider: FakeDateProvider.Default
		);

		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_transactionBuilder = new TransactionBuilder(context: Context);
	}

	private async Task SeedRateAsync(
		Core.ValueObjects.Currency baseCode,
		Core.ValueObjects.Currency targetCode,
		decimal rate,
		DateOnly date,
		DateTimeOffset createdAt)
	{
		await Context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity()
		{
			BaseCode = baseCode,
			TargetCode = targetCode,
			Rate = rate,
			ActualAt = date,
			CreatedAt = createdAt
		});
		await Context.SaveChangesAsync();
	}

	private async Task SeedTransactionsAsync(
		Guid userId,
		Guid accountId,
		Guid categoryId,
		int count,
		decimal amount,
		string currency)
	{
		DateTimeOffset occurredAt = FakeDateProvider.Default.UtcNow;

		List<TransactionEntity> transactions = Enumerable.Range(start: 0, count: count).Select(selector: _ => new TransactionEntity
		{
			Id = Guid.CreateVersion7(),
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Currency = Core.ValueObjects.Currency.Create(value: currency).Value,
			BaseCurrency = Core.ValueObjects.Currency.Create(value: "RUB").Value,
			Direction = DirectionType.Debit,
			ExchangeRate = 1m,
			IsExcluded = false,
			RateStatus = RateStatus.Exact,
			RateStatusChangedAt = occurredAt,
			Description = null,
			OccurredAt = occurredAt
		}).ToList();

		await Context.Transactions.AddRangeAsync(entities: transactions);
		await Context.SaveChangesAsync();
	}

	private async Task SeedCategoryTotalAsync(
		Guid userId,
		Guid categoryId,
		decimal total)
	{
		await Context.CategoryTotals.AddAsync(entity: new CategoryTotalEntity
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			CategoryId = categoryId,
			Period = new DateOnly(
				year: FakeDateProvider.Default.UtcNow.Year,
				month: FakeDateProvider.Default.UtcNow.Month,
				day: 1
			),
			Total = total,
			TransactionCount = 1,
			RowVersion = 0,
			UpdatedAt = FakeDateProvider.Default.UtcNow
		});
		await Context.SaveChangesAsync();
	}

	[Test]
	public async Task AddAsync_WhenNoRecordExists_ShouldCreateNewRecord()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			amount: 1000m,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		CategoryTotalEntity? entity = await Context.CategoryTotals.FirstOrDefaultAsync(predicate: ct =>
			ct.UserId == userId &&
			ct.CategoryId == categoryId &&
			ct.Period == new DateOnly(year: 2025, month: 1, day: 1)
		);

		await Assert.That(value: entity).IsNotNull();
		await Assert.That(value: entity!.Total).IsEqualTo(expected: 1000m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task AddAsync_WhenRecordExists_ShouldAccumulateTotal()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 500m,
			occurredAt: occurredAt
		);

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 1500m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task SubtractAsync_ShouldDecreaseTotal()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _writeRepository.SubtractAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 400m,
			occurredAt: occurredAt
		);

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 600m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task AddAsync_ShouldSeparatePeriodsByMonth()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 2000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 2, day: 10, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		int count = await Context.CategoryTotals.CountAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task ChangeCategoryAsync_ShouldSubtractFromOldAndAddToNew()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid oldCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid newCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: oldCategoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _writeRepository.ChangeCategoryAsync(
			userId: userId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 1000m,
			occurredAt: occurredAt
		);

		CategoryTotalEntity oldEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == oldCategoryId
		);
		CategoryTotalEntity newEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == newCategoryId
		);

		await Assert.That(value: oldEntity.Total).IsEqualTo(expected: 0m);
		await Assert.That(value: newEntity.Total).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task AddThenSubtract_WhenANewerRateAppearsInBetween_ShouldStillNetToZero()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _currencyBuilder.CreateAsync(code: "USD");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		DateTimeOffset transactionOccurredAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 90m,
			date: new DateOnly(year: 2025, month: 1, day: 9),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 95m,
			date: new DateOnly(year: 2025, month: 1, day: 10),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.SubtractAsync(
			userId: userId,
			categoryId: categoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
		);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 0m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task ChangeCategoryAsync_WhenANewerRateAppearsInBetween_ShouldStillMoveExactAmount()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid oldCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
		Guid newCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
		await _currencyBuilder.CreateAsync(code: "USD");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		DateTimeOffset transactionOccurredAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 90m,
			date: new DateOnly(year: 2025, month: 1, day: 9),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 9, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: oldCategoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		await SeedRateAsync(
			baseCode: usd,
			targetCode: rub,
			rate: 95m,
			date: new DateOnly(year: 2025, month: 1, day: 10),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.ChangeCategoryAsync(
			userId: userId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currency: usd,
			amount: 100m,
			occurredAt: transactionOccurredAt
		);

		CategoryTotalEntity oldEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == oldCategoryId
		);
		CategoryTotalEntity newEntity = await Context.CategoryTotals.FirstAsync(
			predicate: ct => ct.UserId == userId && ct.CategoryId == newCategoryId
		);

		await Assert.That(value: oldEntity.Total).IsEqualTo(expected: 0m);
		await Assert.That(value: newEntity.Total).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_WhenNoTransactions_ShouldLeaveNoCategoryTotals()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "USD").Value
		);

		int count = await Context.CategoryTotals.CountAsync(predicate: ct => ct.UserId == userId);
		await Assert.That(value: count).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_ShouldReplaceExistingTotals()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 999_999m,
			occurredAt: new DateTimeOffset(year: 2024, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1000m,
			currencyCode: "RUB",
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);
		await Context.SaveChangesAsync();

		List<CategoryTotalEntity> totals = await Context.CategoryTotals.Where(predicate: c => c.UserId == userId).ToListAsync();

		await Assert.That(value: totals.Count).IsEqualTo(expected: 1)
			.Because(message: "The stale pre-recalculation row must be gone, not summed with the new one.");
		await Assert.That(value: totals[0].Total).IsEqualTo(expected: 1000m);
		await Assert.That(value: totals[0].TransactionCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_ShouldGroupByCategoryAndMonth()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 100m, currencyCode: "RUB",
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 5, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 200m, currencyCode: "RUB",
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 20, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 300m, currencyCode: "RUB",
			occurredAt: new DateTimeOffset(year: 2025, month: 2, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);
		await Context.SaveChangesAsync();

		List<CategoryTotalEntity> totals = await Context.CategoryTotals.Where(predicate: c => c.UserId == userId)
			.OrderBy(keySelector: c => c.Period).ToListAsync();

		await Assert.That(value: totals.Count).IsEqualTo(expected: 2);
		await Assert.That(value: totals[0].Period).IsEqualTo(expected: new DateOnly(year: 2025, month: 1, day: 1));
		await Assert.That(value: totals[0].Total).IsEqualTo(expected: 300m);
		await Assert.That(value: totals[0].TransactionCount).IsEqualTo(expected: 2);
		await Assert.That(value: totals[1].Period).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 1));
		await Assert.That(value: totals[1].Total).IsEqualTo(expected: 300m);
		await Assert.That(value: totals[1].TransactionCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_ShouldExcludeExcludedTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 100m, currencyCode: "RUB", isExcluded: false,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 5, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 5000m, currencyCode: "RUB", isExcluded: true,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 6, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);
		await Context.SaveChangesAsync();

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(predicate: c => c.UserId == userId);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 100m);
		await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_WithSameCurrencyAsBase_ShouldNotRequireAnyRate()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 250m, currencyCode: "RUB",
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 5, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value
		);
		await Context.SaveChangesAsync();

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(predicate: c => c.UserId == userId);
		await Assert.That(value: entity.Total).IsEqualTo(expected: 250m);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_ShouldUseTheRateKnownAtOrBeforeEachTransactionsOwnDate()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _currencyBuilder.CreateAsync(code: "USD");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		await SeedRateAsync(
			baseCode: usd, targetCode: rub, rate: 90m,
			date: new DateOnly(year: 2025, month: 1, day: 1),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 1, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await SeedRateAsync(
			baseCode: usd, targetCode: rub, rate: 100m,
			date: new DateOnly(year: 2025, month: 2, day: 1),
			createdAt: new DateTimeOffset(year: 2025, month: 2, day: 1, hour: 18, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 10m, currencyCode: "USD",
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 20, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateAllForUserAsync(userId: userId, baseCurrency: rub);
		await Context.SaveChangesAsync();

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(predicate: c => c.UserId == userId);
		await Assert.That(value: entity.Total).IsEqualTo(expected: 900m);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_WhenNoRateIsKnownForATransaction_ShouldThrowCurrencyRateMissingException()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _currencyBuilder.CreateAsync(code: "JPY");

		await _transactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			amount: 1000m, currencyCode: "JPY",
			occurredAt: new DateTimeOffset(year: 2019, month: 6, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		CurrencyRateMissingException? exception = await Assert.ThrowsAsync<CurrencyRateMissingException>(action: async () =>
			await _writeRepository.RecalculateAllForUserAsync(
				userId: userId,
				baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value
			)
		);

		await Assert.That(value: exception).IsNotNull();
		await Assert.That(value: exception!.FromCurrency).IsEqualTo(expected: Core.ValueObjects.Currency.Reconstitute(value: "JPY"));
		await Assert.That(value: exception.ToCurrency).IsEqualTo(expected: Core.ValueObjects.Currency.Reconstitute(value: "RUB"));
	}

	[Test]
	public async Task RecalculateAllForUserAsync_ShouldRoundEachTransactionBeforeSumming_NotAfter()
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "RUB");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _currencyBuilder.CreateAsync(code: "USD");

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		await SeedRateAsync(
			baseCode: usd, targetCode: rub, rate: 0.004m,
			date: new DateOnly(year: 2025, month: 1, day: 1),
			createdAt: new DateTimeOffset(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _transactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, amount: 1m, currencyCode: "USD", occurredAt: occurredAt);
		await _transactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, amount: 1m, currencyCode: "USD", occurredAt: occurredAt);

		await _writeRepository.RecalculateAllForUserAsync(userId: userId, baseCurrency: rub);
		await Context.SaveChangesAsync();

		CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(predicate: c => c.UserId == userId);

		await Assert.That(value: entity.Total).IsEqualTo(expected: 0.00m)
			.Because(message: "Each transaction must round to 0.00 before summing — summing the raw amounts first would wrongly produce 0.01.");
	}

[Test]
	public async Task RecalculateAllForUserAsync_WithMoreTransactionsThanOnePage_ShouldTotalThemAll()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		const int transactionCount = 250;
		await SeedTransactionsAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			count: transactionCount,
			amount: 10m,
			currency: "RUB"
		);

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Reconstitute(value: "RUB"),
			ct: CancellationToken.None
		);
		await Context.SaveChangesAsync();

		CategoryTotalEntity total = await Context.CategoryTotals.AsNoTracking().SingleAsync(predicate: t => t.UserId == userId);

		await Assert.That(value: total.TransactionCount).IsEqualTo(expected: transactionCount).Because(message: """
			With a page size of 100 this history spans three pages, so a cursor that does not advance
			correctly shows up as a count stopping at a page boundary.
		""");
		await Assert.That(value: total.Total).IsEqualTo(expected: transactionCount * 10m);
	}

	[Test]
	public async Task RecalculateAllForUserAsync_WithNoTransactions_ShouldClearExistingTotals()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await SeedCategoryTotalAsync(userId: userId, categoryId: categoryId, total: 500m);

		await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Reconstitute(value: "RUB"),
			ct: CancellationToken.None
		);
		await Context.SaveChangesAsync();

		int remaining = await Context.CategoryTotals.AsNoTracking().CountAsync(predicate: t => t.UserId == userId);

		await Assert.That(value: remaining).IsEqualTo(expected: 0).Because(message: """
			Nothing to accumulate still means the stored totals are wrong and have to go. Skipping the
			delete when there is nothing to insert would leave them behind for good.
		""");
	}

	[Test]
	public async Task RecalculateAllForUserAsync_WithAMissingRate_ShouldLeaveExistingTotalsIntact()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId, currencyCode: "USD");
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await SeedTransactionsAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			count: 1,
			amount: 100m,
			currency: "USD"
		);
		await SeedCategoryTotalAsync(userId: userId, categoryId: categoryId, total: 500m);

		await Assert.That(action: async () => await _writeRepository.RecalculateAllForUserAsync(
			userId: userId,
			baseCurrency: Core.ValueObjects.Currency.Reconstitute(value: "RUB"),
			ct: CancellationToken.None
		)).Throws<CurrencyRateMissingException>();

		int remaining = await Context.CategoryTotals.AsNoTracking().CountAsync(predicate: t => t.UserId == userId);

		await Assert.That(value: remaining).IsEqualTo(expected: 1).Because(message: """
			The rate is checked before the old totals are cleared. Wiping them first and discovering the
			problem afterwards leaves the user with nothing, and only an enclosing transaction to save it.
		""");
	}
}
