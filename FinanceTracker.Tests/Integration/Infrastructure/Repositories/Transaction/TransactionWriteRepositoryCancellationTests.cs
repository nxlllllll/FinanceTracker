using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Operation;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Transaction;

public sealed class TransactionWriteRepositoryCancellationTests : DatabaseFixture
{
	private static readonly DateTimeOffset RecordedAt = FakeDateProvider.Default.UtcNow;
	private static readonly TimeSpan Window = TimeSpan.FromDays(value: 30);

	private TransactionWriteRepository _writeRepository = null!;
	private AccountWriteRepository _accountWriteRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new TransactionWriteRepository(context: Context, operationRepository: new OperationWriteRepository(context: Context));
		_accountWriteRepository = new AccountWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private async Task<(Guid accountId, Guid categoryId, Guid userId)> CreateAccountAndCategoryAsync()
	{
		Core.ValueObjects.Currency currency = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currency);

		Guid accountId = Guid.CreateVersion7();
		await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: userId,
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: currency,
			Balance: 10000m,
			Version: 1,
			OccurredAt: RecordedAt
		));

		Guid categoryId = Guid.CreateVersion7();
		await Context.Categories.AddAsync(entity: new CategoryEntity()
		{
			Id = categoryId,
			UserId = userId,
			ParentId = null,
			Name = Name.Create(value: "Еда").Value,
			Type = CategoryType.Expense,
			IsArchived = false,
			RowVersion = 0,
			CreatedAt = RecordedAt
		});
		await Context.SaveChangesAsync();

		return (accountId, categoryId, userId);
	}

	private static Core.Domains.Transaction.Transaction Build(
		Guid accountId,
		Guid userId,
		Guid categoryId,
		RateStatus rateStatus = RateStatus.Exact,
		bool isExcluded = false,
		int rowVersion = 0,
		Guid? id = null
	) => Core.Domains.Transaction.Transaction.Reconstitute(
		id: id ?? Guid.CreateVersion7(),
		accountId: accountId,
		userId: userId,
		categoryId: categoryId,
		amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
		baseCurrency: Core.ValueObjects.Currency.Create(value: "RUB").Value,
		direction: DirectionType.Debit,
		exchangeRate: 1m,
		isExcluded: isExcluded,
		isCancelled: false,
		cancelledAt: null,
		description: "тест",
		rateStatus: rateStatus,
		rateStatusChangedAt: RecordedAt,
		rowVersion: rowVersion,
		createdAt: RecordedAt,
		occurredAt: RecordedAt
	);

	private async Task<Core.Domains.Transaction.Transaction> CreateAndCancelAsync(
		Guid accountId,
		Guid userId,
		Guid categoryId,
		RateStatus rateStatus = RateStatus.Exact,
		bool isExcluded = false)
	{
		Core.Domains.Transaction.Transaction created = Build(accountId: accountId, userId: userId, categoryId: categoryId, rateStatus: rateStatus);

		await _writeRepository.CreateAsync(transaction: created);
		await Context.SaveChangesAsync();

		int rowVersion = 0;

		if (isExcluded)
		{
			await _writeRepository.ExcludeAsync(transactionId: created.Id, userId: userId, expectedVersion: 0);
			rowVersion = 1;
		}

		Core.Domains.Transaction.Transaction loaded = Build(
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			rateStatus: rateStatus,
			isExcluded: isExcluded,
			rowVersion: rowVersion,
			id: created.Id
		);

		loaded.Cancel(cancelledAt: RecordedAt.AddDays(days: 1), maxAge: Window);
		return loaded;
	}

	[Test]
	public async Task CancelAsync_ShouldFlagTheTransactionAndStampTheTime()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(accountId: accountId, userId: userId, categoryId: categoryId);

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: Guid.CreateVersion7());
		await Context.SaveChangesAsync();

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == cancelled.Id);

		await Assert.That(value: entity.IsCancelled).IsTrue();
		await Assert.That(value: entity.CancelledAt).IsEqualTo(expected: RecordedAt.AddDays(days: 1));
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task CancelAsync_WithAPendingRate_ShouldPersistTheClosedRateStatus()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			rateStatus: RateStatus.Pending
		);

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: Guid.CreateVersion7());
		await Context.SaveChangesAsync();

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == cancelled.Id);

		await Assert.That(value: entity.RateStatus).IsEqualTo(expected: RateStatus.Cancelled).Because(message: """
		BalanceAdjustmentJob selects rows on RateStatus.IsOpen(). Writing the flag without the rate columns would leave the
		row in that selection, and the job would post a rate difference to a balance whose movement has already been compensated away.
		""");
	}

	[Test]
	public async Task CancelAsync_ShouldAddAReversalLineWithTheOppositeDirection()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(accountId: accountId, userId: userId, categoryId: categoryId);
		Guid reversalId = Guid.CreateVersion7();

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: reversalId);
		await Context.SaveChangesAsync();

		OperationEntity reversal = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == reversalId);

		await Assert.That(value: reversal.ReversalOfId).IsEqualTo(expected: cancelled.Id);
		await Assert.That(value: reversal.DirectionType).IsEqualTo(expected: "credit").Because(message:
			"The original is a debit. Inverting the direction on the compensating line is what makes the feed read as a minus followed by the plus that undid it."
		);
		await Assert.That(value: reversal.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: reversal.CategoryId).IsEqualTo(expected: categoryId);
		await Assert.That(value: reversal.OccurredAt).IsEqualTo(expected: RecordedAt.AddDays(days: 1)).Because(message: """
		The refund is dated when the money came back, not when the transaction it undoes was dated.
		The pair is linked by reversal_of_id, so it does not need to sit next to its original in the feed.
		""");
	}

	[Test]
	public async Task CancelAsync_ShouldFlagTheOriginalLineAsReverted()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(accountId: accountId, userId: userId, categoryId: categoryId);

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: Guid.CreateVersion7());
		await Context.SaveChangesAsync();

		OperationEntity original = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == cancelled.Id);

		await Assert.That(value: original.IsReverted).IsTrue();
		await Assert.That(value: original.ReversalOfId).IsNull().Because(message:
			"The two markers are not interchangeable: the original carries is_reverted, the compensation carries reversal_of_id. A line holding both would be its own refund."
		);
	}

	[Test]
	public async Task CancelAsync_OfAnExcludedTransaction_ShouldKeepTheReversalExcludedToo()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			isExcluded: true
		);
		Guid reversalId = Guid.CreateVersion7();

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: reversalId);
		await Context.SaveChangesAsync();

		OperationEntity reversal = await Context.Operations.AsNoTracking().FirstAsync(predicate: o => o.Id == reversalId);

		await Assert.That(value: reversal.IsExcluded).IsTrue().Because(message: """
		An excluded transaction contributed nothing to the category totals, so its compensation must contribute nothing either.
		A reversal that counted while its original did not would push the category below zero.
		""");
	}

	[Test]
	public async Task CancelAsync_Twice_ShouldThrowConcurrencyConflictException()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(accountId: accountId, userId: userId, categoryId: categoryId);

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: Guid.CreateVersion7());
		await Context.SaveChangesAsync();

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () => await _writeRepository.CancelAsync(
			transaction: cancelled,
			reversalId: Guid.CreateVersion7()
		));
	}

	[Test]
	public async Task CancelAsync_ShouldLeaveExactlyOneReversalPerTransaction()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction cancelled = await CreateAndCancelAsync(accountId: accountId, userId: userId, categoryId: categoryId);

		await _writeRepository.CancelAsync(transaction: cancelled, reversalId: Guid.CreateVersion7());
		await Context.SaveChangesAsync();

		int reversals = await Context.Operations.AsNoTracking().CountAsync(predicate: o => o.ReversalOfId == cancelled.Id);
		int lines = await Context.Operations.AsNoTracking().CountAsync(predicate: o => o.UserId == userId);

		await Assert.That(value: reversals).IsEqualTo(expected: 1);
		await Assert.That(value: lines).IsEqualTo(expected: 2).Because(message:
			"One movement plus one compensation. A third line would mean the reversal was recorded in rm_transactions as well, which the category-direction invariant forbids."
		);
	}
}
