using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class AccountRevertTransactionTests
{
	private static readonly DateTimeOffset Now = FakeDateProvider.Default.UtcNow;

	private static Result<UnitResult, DomainException> Revert(
		Account account,
		decimal amount,
		DirectionType direction,
		decimal exchangeRate = 1m
	) => account.RevertTransaction(
		occurredAt: Now,
		transactionId: Guid.CreateVersion7(),
		categoryId: Guid.CreateVersion7(),
		amount: amount,
		exchangeRate: exchangeRate,
		direction: direction,
		description: null
	);

	[Test]
	public async Task RevertTransaction_OfADebit_ShouldPutTheMoneyBack()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;
		account.Debit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 3_000m, exchangeRate: 1m, description: null);

		Result<UnitResult, DomainException> result = Revert(account: account, amount: 3_000m, direction: DirectionType.Debit);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10_000m);
	}

	[Test]
	public async Task RevertTransaction_OfACredit_ShouldTakeTheMoneyBack()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;
		account.Credit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 5_000m, exchangeRate: 1m, description: null);

		Result<UnitResult, DomainException> result = Revert(account: account, amount: 5_000m, direction: DirectionType.Credit);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10_000m);
	}

	[Test]
	public async Task RevertTransaction_AtTheOriginalRate_ShouldReturnToTheExactStartingBalance()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;
		account.Debit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 333.33m, exchangeRate: 3.7m, description: null);

		decimal afterDebit = account.Balance.Amount;

		Revert(account: account, amount: 333.33m, direction: DirectionType.Debit, exchangeRate: 3.7m);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10_000m).Because(message: $"""
		The reversal has to undo the movement exactly, not approximately. Both sides go through Money.ConvertedAmount,
		so a rate that does not divide evenly ({afterDebit} after the debit) still lands back on the starting figure rather than a fraction away from it.
		""");
	}

	[Test]
	public async Task RevertTransaction_OfACreditWhoseMoneyIsGone_ShouldFail()
	{
		Account account = AccountFactory.Create(balance: 0m).Value!;
		account.Credit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 5_000m, exchangeRate: 1m, description: null);
		account.Debit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 5_000m, exchangeRate: 1m, description: null);

		Result<UnitResult, DomainException> result = Revert(account: account, amount: 5_000m, direction: DirectionType.Credit);

		await Assert.That(value: result.IsFailure).IsTrue().Because(message: """
		Taking back an income that has already been spent is a debit in everything but name.
		The account refuses overdrafts everywhere else, and this is the one path that would otherwise drive a balance negative.
		""");
		await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task RevertTransaction_OfADebitOnAnEmptyAccount_ShouldSucceed()
	{
		Account account = AccountFactory.Create(balance: 3_000m).Value!;
		account.Debit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 3_000m, exchangeRate: 1m, description: null);

		Result<UnitResult, DomainException> result = Revert(account: account, amount: 3_000m, direction: DirectionType.Debit);

		await Assert.That(value: result.IsSuccess).IsTrue().Because(message: """
		The funds check only guards reverted credits. A reverted debit adds money, so an empty balance is
		no obstacle — this is the common case of undoing a payment that emptied the account.
		""");
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 3_000m);
	}

	[Test]
	public async Task RevertTransaction_OnAnArchivedAccount_ShouldFail()
	{
		Account account = AccountFactory.CreateWithArchivation(balance: 10_000m, archived: true);

		Result<UnitResult, DomainException> result = Revert(account: account, amount: 1_000m, direction: DirectionType.Debit);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task RevertTransaction_DatedBeforeTheAccountExisted_ShouldFail()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;

		Result<UnitResult, DomainException> result = account.RevertTransaction(
			occurredAt: Now.AddDays(days: -1),
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 1_000m,
			exchangeRate: 1m,
			direction: DirectionType.Debit,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<OperationPredatesAccountException>();
	}

	[Test]
	public async Task RevertTransaction_ShouldRaiseTheEventCarryingTheOriginalDirection()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;
		account.ClearEvents();

		Revert(account: account, amount: 1_000m, direction: DirectionType.Debit, exchangeRate: 2.5m);

		AccountTransactionReverted @event = account.Events.OfType<AccountTransactionReverted>().Single();

		await Assert.That(value: @event.Direction).IsEqualTo(expected: DirectionType.Debit).Because(message: """
		The event stores the direction of the transaction being undone, not of the movement it performs.
		The projection inverts it, and swapping the convention here would flip every reverted balance
		in the read model without touching the aggregate.
		""");
		await Assert.That(value: @event.Amount).IsEqualTo(expected: 1_000m);
		await Assert.That(value: @event.ExchangeRate).IsEqualTo(expected: 2.5m).Because(message:
			"The rate travels with the event so the projection recomputes the delta the same way the aggregate did. A pre-computed delta would round twice."
		);
	}
}
