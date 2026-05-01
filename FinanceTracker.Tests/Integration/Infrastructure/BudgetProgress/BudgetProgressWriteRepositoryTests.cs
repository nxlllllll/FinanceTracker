using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
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

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _currencyConversionService = Substitute.For<ICurrencyConversionService>();
        _currencyConversionService.GetConversionRateAsync(
            fromCurrency: Arg.Any<string>(),
            toCurrency: Arg.Any<string>(),
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
            currencyCode: "RUB",
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

        await _writeRepository.AddAsync(userId: userId, categoryId: categoryId, currencyCode: "RUB", amount: 3000m, occurredAt: occurredAt);
        await _writeRepository.AddAsync(userId: userId, categoryId: categoryId, currencyCode: "RUB", amount: 2000m, occurredAt: occurredAt);

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

        await _writeRepository.AddAsync(userId: userId, categoryId: categoryId, currencyCode: "RUB", amount: 5000m, occurredAt: occurredAt);
        await _writeRepository.SubtractAsync(userId: userId, categoryId: categoryId, currencyCode: "RUB", amount: 2000m, occurredAt: occurredAt);

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
            currencyCode: "RUB",
            amount: 3000m,
            occurredAt: new DateTime(year: 2025, month: 2, day: 15, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );

        BudgetProgressEntity progress = await Context.BudgetProgresses.AsNoTracking().FirstAsync(
            predicate: p => p.BudgetId == budgetId
        );

        await Assert.That(value: progress.Spent).IsEqualTo(expected: 0m);
    }
}