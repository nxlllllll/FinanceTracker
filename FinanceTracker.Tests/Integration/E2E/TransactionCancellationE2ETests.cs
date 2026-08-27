using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: CancelTransaction → event store → outbox → RabbitMQ → AccountEventsConsumer → balance restored.
/// </summary>
public sealed class TransactionCancellationE2ETests : E2EFixture
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
			Name: Name.Create(value: "Тестовый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			InitialBalance: balance
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Guid accountId = result.Value;

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		return accountId;
	}

	private async Task<Guid> CreateTransactionAsync(
		Guid userId,
		Guid accountId,
		Guid categoryId,
		decimal amount,
		decimal expectedBalanceAfter)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: amount,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: DirectionType.Debit,
			Description: null,
			OccurredAt: DateTimeOffset.UtcNow
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await RunOutboxAsync();
		await WaitForBalanceAsync(accountId: accountId, expected: expectedBalanceAfter);

		return result.Value;
	}

	private Task WaitForBalanceAsync(Guid accountId, decimal expected) => WaitForConditionAsync(condition: async () =>
	{
		await using FinanceTrackerContext ctx = CreateReadContext();
		decimal balance = await ctx.AccountBalances.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstOrDefaultAsync();
		return balance == expected;
	});

	[Test]
	public async Task CancelTransaction_AfterProjection_ShouldPutTheMoneyBack()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);

		Guid transactionId = await CreateTransactionAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3_000m,
			expectedBalanceAfter: 7_000m
		);

		Result<Guid, AppException> cancelled = await Mediator.Send(request: new CancelTransactionCommand(
			UserId: userId,
			TransactionId: transactionId
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await RunOutboxAsync();
		await WaitForBalanceAsync(accountId: accountId, expected: 10_000m);

		await using FinanceTrackerContext readCtx = CreateReadContext();
		decimal balance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: cancelled.IsSuccess).IsTrue();
		await Assert.That(value: balance).IsEqualTo(expected: 10_000m)
			.Because(message: "The compensating movement has to land back on the figure the account held before the transaction, not near it. Both the debit and its reversal go through Money.ConvertedAmount, so the pair nets to zero exactly.");
	}

	[Test]
	public async Task CancelTransaction_ShouldAdvanceTheAccountVersionForConditionalWrites()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);

		Guid transactionId = await CreateTransactionAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3_000m,
			expectedBalanceAfter: 7_000m
		);

		int versionBeforeCancel;

		await using (FinanceTrackerContext before = CreateReadContext())
		{
			versionBeforeCancel = await before.Accounts.Where(predicate: a => a.Id == accountId)
				.Select(selector: a => a.LastVersion)
				.FirstAsync();
		}

		await Mediator.Send(request: new CancelTransactionCommand(UserId: userId, TransactionId: transactionId)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await RunOutboxAsync();
		await WaitForBalanceAsync(accountId: accountId, expected: 10_000m);

		await using FinanceTrackerContext readCtx = CreateReadContext();
		int versionAfterCancel = await readCtx.Accounts.Where(predicate: a => a.Id == accountId)
			.Select(selector: a => a.LastVersion)
			.FirstAsync();

		await Assert.That(value: versionAfterCancel).IsGreaterThan(minimum: versionBeforeCancel)
			.Because(message: "accounts.last_version is the counter behind both the ETag and the If-Match check. A reversal that moved the balance without moving the version would leave every conditional write on this account failing, which is the defect the single-counter change was made to remove.");
	}

	[Test]
	public async Task CancelTransaction_ShouldShowBothLinesInTheHistoryFeed()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);

		Guid transactionId = await CreateTransactionAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3_000m,
			expectedBalanceAfter: 7_000m
		);

		await Mediator.Send(request: new CancelTransactionCommand(UserId: userId, TransactionId: transactionId)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Result<PagedResult<Operation>, AppException> history = await Mediator.Send(request: new GetOperationsHistoryQuery(
			UserId: userId
		));

		IReadOnlyList<Operation> items = history.Value!.Items;

		Operation original = items.Single(predicate: o => o.Id == transactionId);
		Operation reversal = items.Single(predicate: o => o.ReversalOfId == transactionId);

		await Assert.That(value: items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: original.IsReverted).IsTrue();
		await Assert.That(value: original.Transaction!.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: reversal.Transaction!.Direction).IsEqualTo(expected: DirectionType.Credit)
			.Because(message: "The feed is meant to read as the money leaving and coming back, so the compensation carries the opposite direction of the line it undoes.");
		await Assert.That(value: reversal.Transaction.Amount).IsEqualTo(expected: 3_000m);
	}

	[Test]
	public async Task CancelTransaction_WithTheSameIdempotencyKey_ShouldNotRefundTwice()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);

		Guid transactionId = await CreateTransactionAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3_000m,
			expectedBalanceAfter: 7_000m
		);

		Guid idempotencyKey = Guid.CreateVersion7();

		await Mediator.Send(request: new CancelTransactionCommand(UserId: userId, TransactionId: transactionId)
		{ IdempotencyKey = idempotencyKey });

		Result<Guid, AppException> replay = await Mediator.Send(request: new CancelTransactionCommand(
			UserId: userId,
			TransactionId: transactionId
		)
		{ IdempotencyKey = idempotencyKey });

		await RunOutboxAsync();
		await WaitForBalanceAsync(accountId: accountId, expected: 10_000m);

		await using FinanceTrackerContext readCtx = CreateReadContext();
		decimal balance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();
		int reversals = await readCtx.Operations.CountAsync(predicate: o => o.ReversalOfId == transactionId);

		await Assert.That(value: replay.IsSuccess).IsTrue()
			.Because(message: "A retried request must get the first response back, not the domain's refusal to cancel twice. A client whose response was dropped is asking again for the same outcome, not for a second refund.");
		await Assert.That(value: balance).IsEqualTo(expected: 10_000m);
		await Assert.That(value: reversals).IsEqualTo(expected: 1);
	}
}
