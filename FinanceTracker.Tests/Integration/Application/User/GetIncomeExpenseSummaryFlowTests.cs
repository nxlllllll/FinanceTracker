using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.User.Queries.GetIncomeExpenseSummary;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;

namespace FinanceTracker.Tests.Integration.Application.User;

public sealed class GetIncomeExpenseSummaryFlowTests : MediatorFixture
{
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
	}

	private async Task<Guid> CreateAccountAsync(Guid userId, decimal balance = 10_000m)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateAccountCommand(
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
		decimal amount,
		DirectionType direction,
		DateTimeOffset occurredAt)
	{
		return new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: amount,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: direction,
			Description: null,
			OccurredAt: occurredAt
		) { IdempotencyKey = Guid.CreateVersion7() };
	}

	[Test]
	public async Task CreateCreditTransaction_ThenGetSummary_ShouldReportNonZeroIncome()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 0m);
		Guid incomeCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Зарплата", type: CategoryType.Income);

		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 6, day: 10, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		Result<Guid, AppException> transactionResult = await Mediator.Send(request: BuildCommand(
			userId: userId,
			accountId: accountId,
			categoryId: incomeCategoryId,
			amount: 10_000m,
			direction: DirectionType.Credit,
			occurredAt: occurredAt
		));
		await Assert.That(value: transactionResult.IsSuccess).IsTrue();

		Result<IncomeExpenseSummary, AppException> summaryResult = await Mediator.Send(request: new GetIncomeExpenseSummaryQuery(
			UserId: userId,
			Period: new DateOnly(year: 2025, month: 6, day: 1)
		));

		await Assert.That(value: summaryResult.IsSuccess).IsTrue();
		await Assert.That(value: summaryResult.Value!.Income).IsEqualTo(expected: 10_000m);
		await Assert.That(value: summaryResult.Value!.Expense).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task CreateDebitTransaction_ThenGetSummary_ShouldReportNonZeroExpenseAndZeroIncome()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);
		Guid expenseCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда", type: CategoryType.Expense);

		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 6, day: 12, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await Mediator.Send(request: BuildCommand(
			userId: userId,
			accountId: accountId,
			categoryId: expenseCategoryId,
			amount: 3_000m,
			direction: DirectionType.Debit,
			occurredAt: occurredAt
		));

		Result<IncomeExpenseSummary, AppException> summaryResult = await Mediator.Send(request: new GetIncomeExpenseSummaryQuery(
			UserId: userId,
			Period: new DateOnly(year: 2025, month: 6, day: 1)
		));

		await Assert.That(value: summaryResult.Value!.Income).IsEqualTo(expected: 0m);
		await Assert.That(value: summaryResult.Value!.Expense).IsEqualTo(expected: 3_000m);
	}

	[Test]
	public async Task CreateCreditAndDebitTransactions_ThenGetSummary_ShouldReportBothIndependently()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 0m);
		Guid incomeCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Зарплата", type: CategoryType.Income);
		Guid expenseCategoryId = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда", type: CategoryType.Expense);

		DateTimeOffset occurredAt = new DateTimeOffset(year: 2025, month: 7, day: 5, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await Mediator.Send(request: BuildCommand(
			userId: userId, accountId: accountId, categoryId: incomeCategoryId,
			amount: 50_000m, direction: DirectionType.Credit, occurredAt: occurredAt
		));
		await Mediator.Send(request: BuildCommand(
			userId: userId, accountId: accountId, categoryId: expenseCategoryId,
			amount: 12_000m, direction: DirectionType.Debit, occurredAt: occurredAt
		));

		Result<IncomeExpenseSummary, AppException> summaryResult = await Mediator.Send(request: new GetIncomeExpenseSummaryQuery(
			UserId: userId,
			Period: new DateOnly(year: 2025, month: 7, day: 1)
		));

		await Assert.That(value: summaryResult.Value!.Income).IsEqualTo(expected: 50_000m);
		await Assert.That(value: summaryResult.Value!.Expense).IsEqualTo(expected: 12_000m);
	}
}
