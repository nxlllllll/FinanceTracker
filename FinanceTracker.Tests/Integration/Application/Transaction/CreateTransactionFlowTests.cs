using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Application.Transaction;

/// <summary>
/// Flow tests: CreateTransaction → Event Store → balance updated → budget progress updated.
/// The account is created through Mediator (not through Builder) so that it is in the Event Store
/// and AccountLoader can download it during authorization.
/// </summary>
public sealed class CreateTransactionFlowTests : MediatorFixture
{
    private UserBuilder _userBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;
    private BudgetBuilder _budgetBuilder = null!;

    [Before(hookType: Test)]
    public async Task SetupDataAsync()
    {
        _userBuilder = new UserBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
        _budgetBuilder = new BudgetBuilder(context: Context);
        await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
    }

    /// <summary>Creates an account through MediatR and gets into the Event Store and read model.</summary>
    private async Task<Guid> CreateAccountAsync(Guid userId, decimal balance = 10_000m)
    {
        Result<Guid, DomainException> result = await Mediator.Send(request: new CreateAccountCommand(
            UserId: userId,
            Name: Name.Create(value: "Основной счёт").Value,
            Type: AccountType.Checking,
            Currency: Currency.Create(value: "RUB").Value,
            InitialBalance: balance
        ) { IdempotencyKey = Guid.CreateVersion7() });

        Guid accountId = result.Value!;

        await Context.Accounts.AddAsync(new AccountEntity
        {
            Id = accountId,
            UserId = userId,
            Name = Name.Create(value: "Основной счёт").Value,
            AccountType = AccountType.Checking,
            Currency = Currency.Create(value: "RUB").Value,
            IsArchived = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Context.AccountBalances.AddAsync(new AccountBalanceEntity
        {
            AccountId = accountId,
            Balance = balance,
            LastVersion = 1,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await Context.SaveChangesAsync();

        return accountId;
    }

    private CreateTransactionCommand BuildCommand(
        Guid userId,
        Guid accountId,
        Guid categoryId,
        decimal amount = 1_000m,
        DirectionType direction = DirectionType.Debit)
    {
        return new CreateTransactionCommand(
            AccountId: accountId,
            UserId: userId,
            CategoryId: categoryId,
            Amount: amount,
            Currency: Currency.Create(value: "RUB").Value,
            Direction: direction,
            Description: null,
            OccurredAt: DateTimeOffset.UtcNow
        ) { IdempotencyKey = Guid.CreateVersion7() };
    }

    [Test]
    public async Task CreateTransaction_Debit_ShouldSucceed()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        Result<Guid, DomainException> result = await Mediator.Send(
            request: BuildCommand(userId: userId, accountId: accountId, categoryId: categoryId)
        );

        await Assert.That(value: result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task CreateTransaction_Debit_ShouldPersistEventInEventStore()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        await Mediator.Send(request: BuildCommand(userId: userId, accountId: accountId, categoryId: categoryId));

        await using FinanceTrackerContext readCtx = CreateReadContext();
        bool hasDebitEvent = await readCtx.Events.AnyAsync(
            predicate: e => e.AggregateId == accountId && e.EventType == "account.debited"
        );

        await Assert.That(value: hasDebitEvent).IsTrue();
    }

    [Test]
    public async Task CreateTransaction_Debit_ShouldReduceAccountBalance()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        await Mediator.Send(request: BuildCommand(
            userId: userId,
            accountId: accountId, 
            categoryId: categoryId, 
            amount: 3_000m
        ));

        await ProjectAccountEventsAsync(accountId: accountId);

        await using FinanceTrackerContext readCtx = CreateReadContext();
        decimal balance = await readCtx.AccountBalances
            .Where(b => b.AccountId == accountId)
            .Select(b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 7_000m);
    }

    [Test]
    public async Task CreateTransaction_Debit_ShouldUpdateBudgetProgress()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        DateOnly today = DateOnly.FromDateTime(dateTime: DateTime.UtcNow);
        await _budgetBuilder.CreateAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 5_000m,
            dateFrom: today.AddDays(value: -1),
            dateTo: today.AddDays(value: 30)
        );

        await Mediator.Send(
            request: BuildCommand(userId: userId, accountId: accountId, categoryId: categoryId, amount: 1_500m)
        );

        await using FinanceTrackerContext readCtx = CreateReadContext();
        decimal spent = await readCtx.BudgetProgresses
            .Join(
                inner: readCtx.Budgets,
                outerKeySelector: p => p.BudgetId,
                innerKeySelector: b => b.Id,
                resultSelector: (p, b) => new { p.Spent, b.CategoryId, b.UserId }
            )
            .Where(predicate: x => x.CategoryId == categoryId && x.UserId == userId)
            .Select(selector: x => x.Spent)
            .FirstOrDefaultAsync();

        await Assert.That(value: spent).IsEqualTo(expected: 1_500m);
    }

    [Test]
    public async Task CreateTransaction_Debit_WithInsufficientFunds_ShouldFail()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 500m);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        Result<Guid, DomainException> result = await Mediator.Send(
            request: BuildCommand(userId: userId, accountId: accountId, categoryId: categoryId, amount: 1_000m)
        );

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>();
    }

    [Test]
    public async Task CreateTransaction_Debit_WithInsufficientFunds_ShouldNotChangeBalance()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 500m);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        await Mediator.Send(
            request: BuildCommand(userId: userId, accountId: accountId, categoryId: categoryId, amount: 1_000m)
        );

        await using FinanceTrackerContext readCtx = CreateReadContext();
        decimal balance = await readCtx.AccountBalances
            .Where(predicate: b => b.AccountId == accountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: balance).IsEqualTo(expected: 500m);
    }

    [Test]
    public async Task CreateTransaction_Credit_ShouldNotUpdateBudgetProgress()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await CreateAccountAsync(userId: userId, balance: 0m);
        Guid categoryId = await _categoryBuilder.CreateAsync(
            userId: userId,
            type: CategoryType.Income
        );

        DateOnly today = DateOnly.FromDateTime(dateTime: DateTime.UtcNow);
        await _budgetBuilder.CreateAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 5_000m,
            dateFrom: today.AddDays(value: -1),
            dateTo: today.AddDays(value: 30)
        );

        await Mediator.Send(request: BuildCommand(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            amount: 2_000m,
            direction: DirectionType.Credit
        ));

        await using FinanceTrackerContext readCtx = CreateReadContext();
        decimal spent = await readCtx.BudgetProgresses
            .Join(
                inner: readCtx.Budgets,
                outerKeySelector: p => p.BudgetId,
                innerKeySelector: b => b.Id,
                resultSelector: (p, b) => new { p.Spent, b.CategoryId }
            )
            .Where(predicate: x => x.CategoryId == categoryId)
            .Select(selector: x => x.Spent)
            .FirstOrDefaultAsync();

        await Assert.That(value: spent).IsEqualTo(expected: 0m);
    }
}