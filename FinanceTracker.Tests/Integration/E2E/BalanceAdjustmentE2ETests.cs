using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: BalanceAdjustmentJob — recalculation of balances based on the exchange rate.
/// Courses are entered into the database directly (real container), transactions/transfers
/// are created through the Builder with is_rate_pending = true.
/// </summary>
/// <remarks> 
/// [NotInParallel]: <see cref="FinanceTracker.Infrastructure.Cache.CachedCurrencyRateReadRepository"/>
/// caches the course in general for all Redis parallel E2E fixtures using the key
/// "rate:{from}:{to}:{date}" without reference to the test run.
/// </remarks>
[NotInParallel(constraintKey: "balance-adjustment-currency-rate")]
public sealed class BalanceAdjustmentE2ETests : E2EFixture
{
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private TransactionBuilder _transactionBuilder = null!;
	private TransferBuilder _transferBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_transactionBuilder = new TransactionBuilder(context: Context);
		_transferBuilder = new TransferBuilder(context: Context);

		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
		await new CurrencyBuilder(context: Context).CreateAsync(code: "USD");
	}

	private async Task<Guid> CreateAccountViaCommandAsync(Guid userId, string currencyCode, decimal balance)
	{
		Result<Guid, DomainException> result = await Mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: currencyCode).Value,
			InitialBalance: balance
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Guid accountId = result.Value!;

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		return accountId;
	}

	private async Task InsertCurrencyRateAsync(string baseCode, string targetCode, decimal rate, DateOnly date)
	{
		await Context.CurrencyRates.AddAsync(new CurrencyRateEntity
		{
			BaseCode = Currency.Reconstitute(value: baseCode),
			TargetCode = Currency.Reconstitute(value: targetCode),
			Rate = rate,
			ActualAt = date,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();
	}

	[Test]
	public async Task Transaction_WithPendingRate_WhenRateAvailable_ShouldAdjustBalance()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 10_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateOnly txDate = DateOnly.FromDateTime(DateTime.UtcNow);
		await InsertCurrencyRateAsync(baseCode: "USD", targetCode: "RUB", rate: 90m, date: txDate);

		// is_rate_pending = true (rate = 1 — placeholder)
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 100m,
			currencyCode: "USD",
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			isRatePending: true,
			occurredAt: DateTimeOffset.UtcNow
		);

		await RunBalanceAdjustmentAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool stillPending = await readCtx.Transactions.Where(predicate: t => t.AccountId == accountId)
			.AnyAsync(predicate: t => t.IsRatePending);

		await Assert.That(value: stillPending).IsFalse();
	}

	[Test]
	public async Task Transaction_WithPendingRate_WhenRateNotAvailable_ShouldSkip()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 5_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		// We do NOT insert the course into the database

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 50m,
			currencyCode: "USD",
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			isRatePending: true,
			occurredAt: DateTimeOffset.UtcNow
		);

		await RunBalanceAdjustmentAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		// is_rate_pending should remain true — the course was not found, the entry was skipped
		bool stillPending = await readCtx.Transactions.Where(predicate: t => t.AccountId == accountId)
			.AnyAsync(predicate: t => t.IsRatePending);

		await Assert.That(value: stillPending).IsTrue();
	}

	[Test]
	public async Task Transfer_WithPendingRate_WhenRateAvailable_ShouldAdjustToAccountBalance()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid fromAccountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "USD", balance: 1_000m);
		Guid toAccountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 0m);

		DateOnly txDate = DateOnly.FromDateTime(DateTime.UtcNow);
		await InsertCurrencyRateAsync(baseCode: "USD", targetCode: "RUB", rate: 92m, date: txDate);

		// is_rate_pending = true (rate = 1 — placeholder)
		await _transferBuilder.CreateAsync(
			userId: userId,
			fromAccountId: fromAccountId,
			currencyFrom: "USD",
			toAccountId: toAccountId,
			currencyTo: "RUB",
			amount: 100m,
			exchangeRate: 1m,
			isRatePending: true,
			occurredAt: DateTimeOffset.UtcNow
		);

		await RunBalanceAdjustmentAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool stillPending = await readCtx.Transfers.Where(predicate: t => t.FromAccountId == fromAccountId)
			.AnyAsync(predicate: t => t.IsRatePending);

		await Assert.That(value: stillPending).IsFalse();
	}

	[Test]
	public async Task MultipleTransactions_OnlyPendingOnes_ShouldBeProcessed()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 20_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateOnly txDate = DateOnly.FromDateTime(DateTime.UtcNow);
		await InsertCurrencyRateAsync(baseCode: "USD", targetCode: "RUB", rate: 88m, date: txDate);

		// is_rate_pending = true (rate = 1 — placeholder)
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 50m,
			currencyCode: "USD",
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			isRatePending: true,
			occurredAt: DateTimeOffset.UtcNow
		);

		// is_rate_pending = true (rate = 1 — placeholder)
		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 200m,
			currencyCode: "RUB",
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			isRatePending: false,
			occurredAt: DateTimeOffset.UtcNow
		);

		await RunBalanceAdjustmentAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int pendingCount = await readCtx.Transactions.Where(predicate: t => t.AccountId == accountId)
			.CountAsync(predicate: t => t.IsRatePending);

		await Assert.That(value: pendingCount).IsEqualTo(expected: 0);
	}
}
