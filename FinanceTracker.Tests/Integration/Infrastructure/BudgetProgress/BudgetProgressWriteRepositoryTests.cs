using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.BudgetProgress;

public sealed class BudgetProgressWriteRepositoryTests : DatabaseFixture
{
    private BudgetProgressWriteRepository _writeRepository = null!;
    private ICurrencyConversionService _currencyConversionService = null!;
    private UserBuilder _userBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;
    private BudgetBuilder _budgetBuilder = null!;
    private AccountBuilder _accountBuilder = null!;
    private TransactionBuilder _transactionBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _currencyConversionService = Substitute.For<ICurrencyConversionService>();
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<Core.ValueObjects.Currency>(),
            toCurrency: Arg.Any<Core.ValueObjects.Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: new ConversionResult(Rate: 1m, IsPending: false));

        _writeRepository = new BudgetProgressWriteRepository(
            context: Context,
            currencyConversionService: _currencyConversionService,
            dateProvider: FakeDateProvider.Default
        );
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
            occurredAt: new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
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
        DateTime occurredAt = new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

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
        DateTime occurredAt = new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

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
            occurredAt: new DateTime(year: 2025, month: 2, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );

        BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
            predicate: p => p.BudgetId == budgetId
        );

        await Assert.That(value: progress.Spent).IsEqualTo(expected: 0m);
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
            occurredAt: new DateTime(year: 2025, month: 1, day: 10, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );
        await _transactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            amount: 2000m,
            occurredAt: new DateTime(year: 2025, month: 1, day: 20, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
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
            occurredAt: new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );
        await _transactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            amount: 9999m,
            occurredAt: new DateTime(year: 2025, month: 2, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
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
        DateTime occurredAt = new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

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
        DateTime occurredAt = new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

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
        DateTime occurredAt = new DateTime(year: 2025, month: 1, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

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