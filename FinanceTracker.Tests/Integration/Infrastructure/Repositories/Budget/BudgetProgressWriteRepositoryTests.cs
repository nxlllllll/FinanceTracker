using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Budget;

public sealed class BudgetProgressWriteRepositoryTests : DatabaseFixture
{
	private BudgetProgressWriteRepository _writeRepository = null!;
	private ICurrencyConversionService _currencyConversionService = null!;
	private IUnitOfWork _unitOfWork = null!;
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private BudgetBuilder _budgetBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private TransactionBuilder _transactionBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_currencyConversionService = Substitute.For<ICurrencyConversionService>();

		_currencyConversionService.GetStableRateAsync(
			fromCurrency: Arg.Any<Core.ValueObjects.Currency>(),
			toCurrency: Arg.Any<Core.ValueObjects.Currency>(),
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 1m);

		_currencyConversionService.GetStableRatesBatchAsync(
			requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<IReadOnlyCollection<CurrencyStableRateRequest>>()!.ToDictionary(
			keySelector: r => r,
			elementSelector: _ => 1m
		));

		_writeRepository = new BudgetProgressWriteRepository(
			context: Context,
			currencyConversionService: _currencyConversionService,
			dateProvider: FakeDateProvider.Default,
			budgetThresholdAlerts: TestBudgetThresholdAlerts.Create(context: Context)
		);
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());

		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_budgetBuilder = new BudgetBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_transactionBuilder = new TransactionBuilder(context: Context);
	}

	private static readonly DateTimeOffset InPeriod = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

	private static Core.ValueObjects.Currency Rub => Core.ValueObjects.Currency.Create(value: "RUB").Value;

	private Task<List<BudgetThresholdReachedEvent>> GetAlertsAsync(Guid budgetId)
		=> TestBudgetThresholdAlerts.ReadStagedAsync(context: Context, budgetId: budgetId);

	[Test]
	public async Task AddAsync_WhenBudgetExists_ShouldIncreaseSpent()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 3000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 3000m);
	}

	[Test]
	public async Task AddAsync_ShouldAccumulateSpent()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 3000m,
			occurredAt: occurredAt
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 2000m,
			occurredAt: occurredAt
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 5000m);
	}

	[Test]
	public async Task AddAsync_WhenMultipleBudgetsOverlapPeriod_ShouldUpdateBothActiveAndInactive()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Guid oldBudgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, isActive: false);
		Guid newBudgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, isActive: true);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 3000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		BudgetProgressEntity oldProgress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(predicate: p => p.BudgetId == oldBudgetId);
		BudgetProgressEntity newProgress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(predicate: p => p.BudgetId == newBudgetId);

		await Assert.That(value: oldProgress.Spent).IsEqualTo(expected: 3000m);
		await Assert.That(value: newProgress.Spent).IsEqualTo(expected: 3000m);
	}

	[Test]
	public async Task AddAsync_WhenReactivatingOldBudget_ShouldAlreadyHaveCorrectProgressWithoutRecalculation()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		Guid budgetAId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, isActive: false);
		Guid budgetBId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, isActive: true);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: rub,
			amount: 4000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		BudgetWriteRepository budgetWriteRepository = new BudgetWriteRepository(context: Context, dateProvider: FakeDateProvider.Default, budgetThresholdAlerts: TestBudgetThresholdAlerts.Create(context: Context));
		await budgetWriteRepository.DeactivateAsync(budgetId: budgetBId, expectedVersion: 0);
		await budgetWriteRepository.ActivateAsync(budgetId: budgetAId, expectedVersion: 1);
		await Context.SaveChangesAsync();

		BudgetProgressEntity progressA = await Context.BudgetProgresses.AsNoTracking().FirstAsync(predicate: p => p.BudgetId == budgetAId);

		await Assert.That(value: progressA.Spent).IsEqualTo(expected: 4000m);
	}

	[Test]
	public async Task AddAsync_WhenTransactionOutsideBudgetPeriod_ShouldNotUpdateProgress()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(
			userId: userId,
			categoryId: categoryId,
			dateFrom: new DateOnly(year: 2025, month: 1, day: 1),
			dateTo: new DateOnly(year: 2025, month: 1, day: 31)
		);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 3000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 2, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task AddAsync_WhenCurrencyDiffersFromBudgetCurrency_ShouldRequestStableRateAnchoredToOccurredAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, currency: "RUB");
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 9, minute: 0, second: 0, offset: TimeSpan.Zero);

		Core.ValueObjects.Currency usd = Core.ValueObjects.Currency.Create(value: "USD").Value;
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;

		_currencyConversionService.GetStableRateAsync(
			fromCurrency: usd,
			toCurrency: rub,
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: usd,
			amount: 100m,
			occurredAt: occurredAt
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 9000m);

		await _currencyConversionService.Received(requiredNumberOfCalls: 1).GetStableRateAsync(
			fromCurrency: usd,
			toCurrency: rub,
			asOf: occurredAt,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task SubtractAsync_WhenMultipleBudgetsOverlapPeriod_ShouldDecreaseBothActiveAndInactive()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Core.ValueObjects.Currency rub = Core.ValueObjects.Currency.Create(value: "RUB").Value;
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		Guid oldBudgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, isActive: false);
		Guid newBudgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, isActive: true);

		await _writeRepository.AddAsync(userId: userId, categoryId: categoryId, currencyCode: rub, amount: 5000m, occurredAt: occurredAt);
		await _writeRepository.SubtractAsync(userId: userId, categoryId: categoryId, currencyCode: rub, amount: 2000m, occurredAt: occurredAt);

		BudgetProgressEntity oldProgress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(predicate: p => p.BudgetId == oldBudgetId);
		BudgetProgressEntity newProgress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(predicate: p => p.BudgetId == newBudgetId);

		await Assert.That(value: oldProgress.Spent).IsEqualTo(expected: 3000m);
		await Assert.That(value: newProgress.Spent).IsEqualTo(expected: 3000m);
	}

	[Test]
	public async Task SubtractAsync_ShouldDecreaseSpent()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 5000m,
			occurredAt: occurredAt
		);
		await _writeRepository.SubtractAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 2000m,
			occurredAt: occurredAt
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 3000m);
	}

	[Test]
	public async Task RecalculateForBudgetAsync_ShouldSumAllDebitTransactionsInPeriod()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(
			userId: userId,
			categoryId: categoryId,
			dateFrom: new DateOnly(year: 2025, month: 1, day: 1),
			dateTo: new DateOnly(year: 2025, month: 1, day: 31)
		);

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 10, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 2000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 20, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateForBudgetAsync(
			budgetId: budgetId,
			userId: userId,
			categoryId: categoryId,
			fromDate: new DateOnly(year: 2025, month: 1, day: 1),
			toDate: new DateOnly(year: 2025, month: 1, day: 31)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 5000m);
	}

	[Test]
	public async Task RecalculateForBudgetAsync_ShouldIgnoreTransactionsOutsidePeriod()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(
			userId: userId,
			categoryId: categoryId,
			dateFrom: new DateOnly(year: 2025, month: 1, day: 1),
			dateTo: new DateOnly(year: 2025, month: 1, day: 31)
		);

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3000m,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 9999m,
			occurredAt: new DateTimeOffset(year: 2025, month: 2, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		await _writeRepository.RecalculateForBudgetAsync(
			budgetId: budgetId,
			userId: userId,
			categoryId: categoryId,
			fromDate: new DateOnly(year: 2025, month: 1, day: 1),
			toDate: new DateOnly(year: 2025, month: 1, day: 31)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 3000m);
	}

	[Test]
	public async Task RecalculateForBudgetAsync_ShouldIgnoreExcludedTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1000m,
			occurredAt: occurredAt
		);
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 9999m,
			isExcluded: true,
			occurredAt: occurredAt
		);

		await _writeRepository.RecalculateForBudgetAsync(
			budgetId: budgetId,
			userId: userId,
			categoryId: categoryId,
			fromDate: new DateOnly(year: 2025, month: 1, day: 1),
			toDate: new DateOnly(year: 2025, month: 1, day: 31)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task RecalculateForBudgetAsync_ShouldIgnoreCreditTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1000m,
			direction: DirectionType.Debit,
			occurredAt: occurredAt
		);
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 9999m,
			direction: DirectionType.Credit,
			occurredAt: occurredAt
		);

		await _writeRepository.RecalculateForBudgetAsync(
			budgetId: budgetId,
			userId: userId,
			categoryId: categoryId,
			fromDate: new DateOnly(year: 2025, month: 1, day: 1),
			toDate: new DateOnly(year: 2025, month: 1, day: 31)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task RecalculateForBudgetAsync_WhenNoTransactionsInPeriod_ShouldResetSpentToZero()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		await _accountBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);
		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Core.ValueObjects.Currency.Create(value: "RUB").Value,
			amount: 5000m,
			occurredAt: occurredAt
		);

		await _writeRepository.RecalculateForBudgetAsync(
			budgetId: budgetId,
			userId: userId,
			categoryId: categoryId,
			fromDate: new DateOnly(year: 2025, month: 2, day: 1),
			toDate: new DateOnly(year: 2025, month: 2, day: 28)
		);

		BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
			predicate: p => p.BudgetId == budgetId
		);

		await Assert.That(value: progress.Spent).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task AddAsync_WhenSpendingCrossesAThreshold_ShouldStageOneAlert()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 8500m,
			occurredAt: InPeriod
		);

		List<BudgetThresholdReachedEvent> alerts = await GetAlertsAsync(budgetId: budgetId);

		await Assert.That(value: alerts).Count().IsEqualTo(expected: 1);
		await Assert.That(value: alerts[0].Threshold).IsEqualTo(expected: 80);
		await Assert.That(value: alerts[0].Spent).IsEqualTo(expected: 8500m);
		await Assert.That(value: alerts[0].Limit).IsEqualTo(expected: 10000m);
		await Assert.That(value: alerts[0].UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task AddAsync_WhenSpendingCrossesSeveralThresholdsAtOnce_ShouldStageOnlyTheHighest()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 12000m,
			occurredAt: InPeriod
		);

		List<BudgetThresholdReachedEvent> alerts = await GetAlertsAsync(budgetId: budgetId);

		await Assert.That(value: alerts).Count().IsEqualTo(expected: 1)
			.Because(message: "an owner told in one breath that the budget is at 80% and at 100% learns nothing from the first");
		await Assert.That(value: alerts[0].Threshold).IsEqualTo(expected: 100);
	}

	[Test]
	public async Task AddAsync_WhenSpendingStaysBelowEveryThreshold_ShouldStageNoAlert()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 5000m,
			occurredAt: InPeriod
		);

		await Assert.That(value: await GetAlertsAsync(budgetId: budgetId)).IsEmpty();
	}

	[Test]
	public async Task AddAsync_WhenAThresholdWasAlreadyCrossed_ShouldNotStageItAgain()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 8500m,
			occurredAt: InPeriod
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 500m,
			occurredAt: InPeriod
		);

		await Assert.That(value: await GetAlertsAsync(budgetId: budgetId)).Count().IsEqualTo(expected: 1).Because(message: """
			Every purchase above 80% would otherwise repeat the same warning. The alert marks the moment
			the level is reached, not the state of being past it.
		""");
	}

	[Test]
	public async Task AddAsync_WhenBudgetIsInactive_ShouldStageNoAlert()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m, isActive: false);

		await _writeRepository.AddAsync(userId: userId, categoryId: categoryId, currencyCode: Rub, amount: 9000m, occurredAt: InPeriod);

		await Assert.That(value: await GetAlertsAsync(budgetId: budgetId)).IsEmpty()
			.Because(message: "an inactive budget still tracks spending so it is accurate when reactivated, but its owner has stopped watching it");
	}

	[Test]
	public async Task AddAsync_AfterSpendingDropsBelowAThreshold_ShouldStageItAgainWhenCrossedAgain()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 8500m,
			occurredAt: InPeriod
		);
		await _writeRepository.SubtractAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 1000m,
			occurredAt: InPeriod
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: categoryId,
			currencyCode: Rub,
			amount: 1000m,
			occurredAt: InPeriod
		);

		List<BudgetThresholdReachedEvent> alerts = await GetAlertsAsync(budgetId: budgetId);

		await Assert.That(value: alerts.Select(selector: a => a.Threshold).ToList()).IsEquivalentTo(expected: [80, 80]).Because(message: """
			A cancelled purchase took the budget back under 80%; the next one that crosses it is news again.
		""");
	}

	[Test]
	public async Task ChangeCategoryAsync_ShouldStageAnAlertOnlyForTheBudgetThatGained()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid oldCategoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid newCategoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid oldBudgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: oldCategoryId, amount: 10000m);
		Guid newBudgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: newCategoryId, amount: 10000m);

		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: oldCategoryId,
			currencyCode: Rub,
			amount: 5000m,
			occurredAt: InPeriod
		);
		await _writeRepository.AddAsync(
			userId: userId,
			categoryId: newCategoryId,
			currencyCode: Rub,
			amount: 5000m,
			occurredAt: InPeriod
		);

		await _writeRepository.ChangeCategoryAsync(
			userId: userId,
			oldCategoryId: oldCategoryId,
			newCategoryId: newCategoryId,
			currencyCode: Rub,
			amount: 4000m,
			occurredAt: InPeriod
		);

		await Assert.That(value: await GetAlertsAsync(budgetId: oldBudgetId)).IsEmpty();
		await Assert.That(value: await GetAlertsAsync(budgetId: newBudgetId)).Count().IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RecalculateForBudgetAsync_WhenRecalculatedSpendingCrossesAThreshold_ShouldStageAnAlert()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

		await _transactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, amount: 9000m, occurredAt: InPeriod);

		await _writeRepository.RecalculateForBudgetAsync(
			budgetId: budgetId,
			userId: userId,
			categoryId: categoryId,
			fromDate: new DateOnly(year: 2025, month: 1, day: 1),
			toDate: new DateOnly(year: 2025, month: 1, day: 31)
		);

		List<BudgetThresholdReachedEvent> alerts = await GetAlertsAsync(budgetId: budgetId);

		await Assert.That(value: alerts).Count().IsEqualTo(expected: 1).Because(message: """
				Widening a budget's period can pull earlier purchases into it. The owner is past 80% as surely
				as if they had just spent the money.
			""");
		await Assert.That(value: alerts[0].Threshold).IsEqualTo(expected: 80);
	}
}
