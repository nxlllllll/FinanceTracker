using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class AccountRevertTransferTests
{
	private static readonly DateTimeOffset Now = FakeDateProvider.Default.UtcNow;

	private static Result<UnitResult, DomainException> RevertDebit(
		Account account,
		decimal amount
	) => account.RevertTransferDebit(
		occurredAt: Now,
		transferId: Guid.CreateVersion7(),
		amount: amount,
		description: null
	);

	private static Result<UnitResult, DomainException> RevertCredit(
		Account account,
		decimal amount,
		decimal exchangeRate = 1m
	) => account.RevertTransferCredit(
		occurredAt: Now,
		transferId: Guid.CreateVersion7(),
		amount: amount,
		exchangeRate: exchangeRate,
		description: null
	);

	[Test]
	public async Task RevertTransferDebit_ShouldPutTheMoneyBackOnTheSource()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;
		account.DebitTransfer(occurredAt: Now, transferId: Guid.CreateVersion7(), toAccountId: Guid.CreateVersion7(), amount: 3_000m, forexRate: 1m, description: null);

		Result<UnitResult, DomainException> result = RevertDebit(account: account, amount: 3_000m);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10_000m);
	}

	[Test]
	public async Task RevertTransferDebit_ShouldRaiseItsOwnEventRatherThanARefund()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;

		RevertDebit(account: account, amount: 500m);

		await Assert.That(value: account.Events.Any(predicate: e => e is AccountTransferDebitReverted)).IsTrue().Because(message: """
			A refund means the credit side failed and the system gave up on its own; a reverted debit means
			a human undid the transfer. Reusing AccountTransferRefunded would make the two indistinguishable
			in the journal, which is the one place the difference has to survive.
		""");

		await Assert.That(value: account.Events.Any(predicate: e => e is AccountTransferRefunded)).IsFalse();
	}

	[Test]
	public async Task RevertTransferCredit_ShouldTakeBackWhatTheTransferBrought()
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;
		account.CreditTransfer(occurredAt: Now, transferId: Guid.CreateVersion7(), fromAccountId: Guid.CreateVersion7(), amount: 100m, exchangeRate: 90m, description: null);

		Result<UnitResult, DomainException> result = RevertCredit(account: account, amount: 100m, exchangeRate: 90m);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10_000m).Because(message: """
			The reversal carries the same amount-and-rate pair the credit did, so it has to land on exactly
			the starting balance. Converting once more, or storing the destination amount with a rate of one,
			would leave a residue that grows with every cancelled transfer.
		""");
	}

	[Test]
	public async Task RevertTransferCredit_WhenTheMoneyIsAlreadySpent_ShouldRefuse()
	{
		Account account = AccountFactory.Create(balance: 0m).Value!;
		account.CreditTransfer(occurredAt: Now, transferId: Guid.CreateVersion7(), fromAccountId: Guid.CreateVersion7(), amount: 500m, exchangeRate: 1m, description: null);
		account.Debit(occurredAt: Now, transactionId: Guid.CreateVersion7(), categoryId: Guid.CreateVersion7(), amount: 500m, exchangeRate: 1m, description: null);

		Result<UnitResult, DomainException> result = RevertCredit(account: account, amount: 500m);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>().Because(message: """
			Taking the money back regardless would drive the destination negative to undo a transfer the
			owner already spent. Refusing keeps the recorded balance true and leaves the decision to a human.
		""");

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task RevertTransferCredit_WithExactlyEnoughLeft_ShouldSucceed()
	{
		Account account = AccountFactory.Create(balance: 0m).Value!;
		account.CreditTransfer(occurredAt: Now, transferId: Guid.CreateVersion7(), fromAccountId: Guid.CreateVersion7(), amount: 500m, exchangeRate: 1m, description: null);

		Result<UnitResult, DomainException> result = RevertCredit(account: account, amount: 500m);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 0m);
	}

	[Test]
	public async Task RevertTransferDebit_OnAnArchivedAccount_ShouldRefuse()
	{
		Account account = AccountFactory.CreateWithArchivation(balance: 10_000m, archived: true);

		Result<UnitResult, DomainException> result = RevertDebit(account: account, amount: 100m);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task RevertTransferCredit_OnAnArchivedAccount_ShouldRefuse()
	{
		Account account = AccountFactory.CreateWithArchivation(balance: 10_000m, archived: true);

		Result<UnitResult, DomainException> result = RevertCredit(account: account, amount: 100m);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	public async Task RevertTransferDebit_WithANonPositiveAmount_ShouldRefuse(int amount)
	{
		Account account = AccountFactory.Create(balance: 10_000m).Value!;

		Result<UnitResult, DomainException> result = RevertDebit(account: account, amount: amount);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}
}
