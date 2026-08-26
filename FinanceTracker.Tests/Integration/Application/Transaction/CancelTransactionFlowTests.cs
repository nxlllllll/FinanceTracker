using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Application.Transaction;

/// <summary>
/// Flow tests: CancelTransaction → cancelled flag, the pair of history lines, and the analytics taken back.
/// </summary>
public sealed class CancelTransactionFlowTests : MediatorFixture
{
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private BudgetBuilder _budgetBuilder = null!;
	private TransactionBuilder _transactionBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_budgetBuilder = new BudgetBuilder(context: Context);
		_transactionBuilder = new TransactionBuilder(context: Context);
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
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Guid accountId = result.Value;

		await Context.Accounts.AddAsync(new AccountEntity
		{
			Id = accountId,
			UserId = userId,
			Name = Name.Create(value: "Основной счёт").Value,
			AccountType = AccountType.Checking,
			Currency = Currency.Create(value: "RUB").Value,
			IsArchived = false,
			LastVersion = 1,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.AccountBalances.AddAsync(new AccountBalanceEntity
		{
			AccountId = accountId,
			Balance = balance,
			UpdatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();

		return accountId;
	}

	private async Task<Guid> CreateTransactionAsync(
		Guid userId,
		Guid accountId,
		Guid categoryId,
		decimal amount = 3_000m,
		DirectionType direction = DirectionType.Debit)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: amount,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: direction,
			Description: null,
			OccurredAt: DateTimeOffset.UtcNow
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		return result.Value;
	}

	private static DateOnly CurrentPeriod()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		return new DateOnly(year: now.Year, month: now.Month, day: 1);
	}

	private Task<Result<Guid, AppException>> CancelAsync(Guid userId, Guid transactionId)
		=> Mediator.Send(request: new CancelTransactionCommand(UserId: userId, TransactionId: transactionId)
		{ IdempotencyKey = Guid.CreateVersion7() });

	[Test]
	public async Task Cancel_ShouldMarkTheTransactionCancelled()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		Result<Guid, AppException> result = await CancelAsync(userId: userId, transactionId: transactionId);

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == transactionId);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: entity.IsCancelled).IsTrue();
		await Assert.That(value: entity.CancelledAt).IsNotNull();
	}

	[Test]
	public async Task Cancel_ShouldAddTheCompensatingLineToTheFeed()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await CancelAsync(userId: userId, transactionId: transactionId);

		OperationEntity original = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == transactionId);
		OperationEntity reversal = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.ReversalOfId == transactionId);

		await Assert.That(value: original.IsReverted).IsTrue();
		await Assert.That(value: reversal.DirectionType).IsEqualTo(expected: "credit");
		await Assert.That(value: reversal.Amount).IsEqualTo(expected: 3_000m);
	}

	[Test]
	public async Task Cancel_ShouldTakeTheAmountBackOutOfTheCategoryTotal()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		DateOnly period = CurrentPeriod();

		decimal beforeCancel = await Context.CategoryTotals.AsNoTracking()
			.Where(predicate: t => t.UserId == userId && t.CategoryId == categoryId && t.Period == period)
			.Select(selector: t => t.Total)
			.FirstAsync();

		await CancelAsync(userId: userId, transactionId: transactionId);

		decimal afterCancel = await Context.CategoryTotals.AsNoTracking()
			.Where(predicate: t => t.UserId == userId && t.CategoryId == categoryId && t.Period == period)
			.Select(selector: t => t.Total)
			.FirstAsync();

		await Assert.That(value: beforeCancel).IsEqualTo(expected: 3_000m);
		await Assert.That(value: afterCancel).IsEqualTo(expected: 0m);
	}

		[Test]
	public async Task Cancel_ShouldTakeTheAmountBackOutOfTheBudgetProgress()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateOnly periodStart = CurrentPeriod();
		Guid budgetId = await _budgetBuilder.CreateAsync(
			userId: userId,
			categoryId: categoryId,
			amount: 50_000m,
			dateFrom: periodStart,
			dateTo: periodStart.AddMonths(value: 1).AddDays(value: -1)
		);

		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		decimal beforeCancel = await Context.BudgetProgresses.AsNoTracking()
			.Where(predicate: p => p.BudgetId == budgetId)
			.Select(selector: p => p.Spent)
			.FirstAsync();

		await CancelAsync(userId: userId, transactionId: transactionId);

		decimal afterCancel = await Context.BudgetProgresses.AsNoTracking()
			.Where(predicate: p => p.BudgetId == budgetId)
			.Select(selector: p => p.Spent)
			.FirstAsync();

		await Assert.That(value: beforeCancel).IsEqualTo(expected: 3_000m);
		await Assert.That(value: afterCancel).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task Cancel_OfAnExcludedTransaction_ShouldLeaveTheCategoryTotalAlone()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await Mediator.Send(request: new ExcludeTransactionCommand(UserId: userId, TransactionId: transactionId));

		DateOnly period = CurrentPeriod();

		decimal afterExclusion = await Context.CategoryTotals.AsNoTracking()
			.Where(predicate: t => t.UserId == userId && t.CategoryId == categoryId && t.Period == period)
			.Select(selector: t => t.Total)
			.FirstAsync();

		Result<Guid, AppException> result = await CancelAsync(userId: userId, transactionId: transactionId);

		decimal afterCancel = await Context.CategoryTotals.AsNoTracking()
			.Where(predicate: t => t.UserId == userId && t.CategoryId == categoryId && t.Period == period)
			.Select(selector: t => t.Total)
			.FirstAsync();

		await Assert.That(value: result.IsSuccess).IsTrue().Because(message:
			"Exclusion governs the analytics, cancellation governs the balance. An excluded transaction still moved money, so it stays cancellable."
		);
		await Assert.That(value: afterExclusion).IsEqualTo(expected: 0m);
		await Assert.That(value: afterCancel).IsEqualTo(expected: 0m).Because(message: """
		Excluding already took the contribution out. Subtracting again on cancellation would
		drive the category below zero by the amount of a movement the total never held.
		""");
	}

	[Test]
	public async Task Cancel_OfAnExcludedTransaction_ShouldStillAddTheCompensatingLine()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await Mediator.Send(request: new ExcludeTransactionCommand(UserId: userId, TransactionId: transactionId));
		await CancelAsync(userId: userId, transactionId: transactionId);

		OperationEntity reversal = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.ReversalOfId == transactionId);

		await Assert.That(value: reversal.IsExcluded).IsTrue().Because(message:
			"The refund inherits the exclusion so the pair stays consistent: neither line counts, and the feed still shows the money going out and coming back."
		);
	}

	[Test]
	public async Task Cancel_Twice_ShouldFail()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid transactionId = await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		await CancelAsync(userId: userId, transactionId: transactionId);
		Result<Guid, AppException> second = await CancelAsync(userId: userId, transactionId: transactionId);

		await Assert.That(value: second.IsFailure).IsTrue();
		await Assert.That(value: second.Error).IsTypeOf<CancelledOperationException>();
	}

	[Test]
	public async Task Cancel_PastTheWindow_ShouldFailWithoutTouchingTheAccount()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Guid accountId = Guid.CreateVersion7();
		Guid transactionId = await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			createdAt: DateTimeOffset.UtcNow.AddDays(days: -60),
			occurredAt: DateTimeOffset.UtcNow.AddDays(days: -60)
		);

		Result<Guid, AppException> result = await CancelAsync(userId: userId, transactionId: transactionId);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<TransactionCancellationWindowExpiredException>();
	}
}
