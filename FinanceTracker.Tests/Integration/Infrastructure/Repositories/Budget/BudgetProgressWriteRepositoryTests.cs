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

        _currencyConversionService.GetStableRatesBatchAsync(
            requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.Arg<IReadOnlyCollection<CurrencyStableRateRequest>>().ToDictionary(
            keySelector: r => r,
            elementSelector: _ => 1m
        ));

        _writeRepository = new BudgetProgressWriteRepository(
            context: Context,
            currencyConversionService: _currencyConversionService,
            dateProvider: FakeDateProvider.Default
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

        _currencyConversionService.GetStableRatesBatchAsync(
            requests: Arg.Any<IReadOnlyCollection<CurrencyStableRateRequest>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.Arg<IReadOnlyCollection<CurrencyStableRateRequest>>().ToDictionary(
            keySelector: r => r,
            elementSelector: _ => 90m
        ));

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

        await _currencyConversionService.Received(requiredNumberOfCalls: 1).GetStableRatesBatchAsync(
            requests: Arg.Is<IReadOnlyCollection<CurrencyStableRateRequest>>(predicate: reqs =>
                reqs.Any(predicate: r => r.From == usd && r.To == rub && r.AsOf == occurredAt)
            ),
            ct: Arg.Any<CancellationToken>()
        );
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
            direction: Core.Domains.Account.DirectionType.Debit, 
            occurredAt: occurredAt
        );
        await _transactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            amount: 9999m,
            direction: Core.Domains.Account.DirectionType.Credit, 
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
}