using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class AccountTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;
	private static readonly AccountSnapshotSerializer Serializer = new AccountSnapshotSerializer();

	[Test]
	public async Task Create_WithValidData_ShouldRaiseAccountCreatedEvent()
	{
		Account account = AccountFactory.Create().Value!;

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountCreated>();
	}

	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.CreateVersion7();

		Account account = AccountFactory.Create(userId: userId, balance: 10000).Value!;

		await Assert.That(value: account.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: account.Name.Value).IsEqualTo(expected: "Карта Сбер");
		await Assert.That(value: account.Type).IsEqualTo(expected: AccountType.Checking);
		await Assert.That(value: account.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10000m);
		await Assert.That(value: account.IsArchived).IsFalse();
		await Assert.That(value: account.Version).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Create_WithEmptyName_ShouldThrowEmptyNameException()
	{
		Result<Account, DomainException> result = AccountFactory.Create(name: String.Empty);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NameException>();
	}

	[Test]
	public async Task Create_WithNegativeBalance_ShouldThrowInvalidInitialBalanceException()
	{
		Result<Account, DomainException> result = AccountFactory.Create(balance: -100);
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidInitialBalanceException>();
	}

	[Test]
	public async Task Rename_WithNewName_ShouldChangeName()
	{
		Account account = AccountFactory.Create().Value!;

		_ = account.Rename(occurredAt: Now, newName: Name.Create(value: "Новое название").Value);

		await Assert.That(value: account.Name.Value).IsEqualTo(expected: "Новое название");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldReturnFalse()
	{
		Account account = AccountFactory.Create(name: "Карта Сбер").Value!;
		account.ClearEvents();

		account.Rename(occurredAt: Now, newName: Name.Create(value: "Карта Сбер").Value);

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Archive_ActiveAccount_ShouldReturnTrueAndSetIsArchived()
	{
		Account account = AccountFactory.Create().Value!;

		account.Archive(occurredAt: Now);

		await Assert.That(value: account.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_AlreadyArchivedAccount_ShouldThrowArchivingException()
	{
		Account account = AccountFactory.Create().Value!;

		account.Archive(occurredAt: Now);
		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Archive(occurredAt: Now);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_ArchivedAccount_ShouldReturnTrueAndClearIsArchived()
	{
		Account account = AccountFactory.Create().Value!;

		account.Archive(occurredAt: Now);
		account.Unarchive(occurredAt: Now);

		await Assert.That(value: account.IsArchived).IsFalse();
	}

	[Test]
	public async Task Unarchive_ActiveAccount_ShouldThrowUnarchivingException()
	{
		Account account = AccountFactory.Create().Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Unarchive(occurredAt: Now);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnarchivingException>();
	}

	[Test]
	public async Task Debit_WithValidData_ShouldRaiseAccountDebitedEvent()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;
		account.ClearEvents();

		account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 1m,
			description: "тест"
		);

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountDebited>();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task Debit_WithExchangeRate_ShouldApplyExchangeRate()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;
		account.ClearEvents();

		account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 90m,
			description: null
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 1000m);
	}

	[Test]
	public async Task Debit_OnArchivedAccount_ShouldThrowArchivingException()
	{
		Account account = AccountFactory.Create().Value!;
		account.Archive(occurredAt: Now);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task Debit_WithZeroAmount_ShouldThrowInvalidAmountException()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 0m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task Credit_WithValidData_ShouldRaiseAccountCreditedEvent()
	{
		Account account = AccountFactory.Create(balance: 1000).Value!;
		account.ClearEvents();

		account.Credit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: "пополнение"
		);

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountCredited>();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 1500m);
	}

	[Test]
	public async Task Credit_OnArchivedAccount_ShouldThrowArchivingException()
	{
		Account account = AccountFactory.Create(balance: 0).Value!;
		account.Archive(occurredAt: Now);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Credit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 1m,
			description: null
		);
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task ReconstituteFromHistory_ShouldRestoreCorrectState()
	{
		Account original = AccountFactory.Create(balance: 10000).Value!;

		original.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 1m,
			description: null
		);

		original.Credit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);

		Account reconstituted = Account.Reconstitute(snapshot: null, events: [.. original.Events]);

		await Assert.That(value: reconstituted.Id).IsEqualTo(expected: original.Id);
		await Assert.That(value: reconstituted.Balance.Amount).IsEqualTo(expected: 9500m);
		await Assert.That(value: reconstituted.Version).IsEqualTo(expected: original.Version);
		await Assert.That(value: reconstituted.Events).Count().IsEqualTo(expected: 0);
	}

	[Test]
	public async Task AdjustBalance_WithDebitAndRateIncrease_ShouldDecreaseBalance()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;
		account.ClearEvents();

		account.AdjustBalance(
			occurredAt: Now,
			sourceId: Guid.CreateVersion7(),
			sourceType: AggregateTypeNames.Transaction,
			direction: DirectionType.Debit,
			oldRate: 85m,
			newRate: 90m,
			amount: 1000m
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 5000m);
		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountBalanceAdjusted>();
	}

	[Test]
	public async Task AdjustBalance_WithCreditAndRateIncrease_ShouldIncreaseBalance()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;
		account.ClearEvents();

		account.AdjustBalance(
			occurredAt: Now,
			sourceId: Guid.CreateVersion7(),
			sourceType: AggregateTypeNames.Transaction,
			direction: DirectionType.Credit,
			oldRate: 85m,
			newRate: 90m,
			amount: 1000m
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 15000m);
	}

	[Test]
	public async Task AdjustBalance_WithDebitAndRateDecrease_ShouldIncreaseBalance()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;
		account.ClearEvents();

		account.AdjustBalance(
			occurredAt: Now,
			sourceId: Guid.CreateVersion7(),
			sourceType: AggregateTypeNames.Transaction,
			direction: DirectionType.Debit,
			oldRate: 90m,
			newRate: 85m,
			amount: 1000m
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 15000m);
	}

	[Test]
	public async Task AdjustBalance_WithSameRate_ShouldNotRaiseEvent()
	{
		Account account = AccountFactory.Create(balance: 10000).Value!;
		account.ClearEvents();

		account.AdjustBalance(
			occurredAt: Now,
			sourceId: Guid.CreateVersion7(),
			sourceType: AggregateTypeNames.Transaction,
			direction: DirectionType.Debit,
			oldRate: 90m,
			newRate: 90m,
			amount: 1000m
		);

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 0);
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10000m);
	}

	[Test]
	public async Task DebitTransfer_ShouldReduceBalanceByAmountOnly_IgnoringForexRate()
	{
		Account account = AccountFactory.Create(balance: 10000m).Value!;
		account.ClearEvents();

		account.DebitTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 1000m,
			forexRate: 0.011m,
			description: null
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 9000m);
		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountTransferDebited>();
	}

	[Test]
	public async Task CreditTransfer_ShouldIncreaseBalanceByAmountMultipliedByExchangeRate()
	{
		Account account = AccountFactory.Create(balance: 0m).Value!;
		account.ClearEvents();

		account.CreditTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			fromAccountId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 0.011m,
			description: null
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 11m);
		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountTransferCredited>();
	}

	[Test]
	public async Task Debit_WithInsufficientFunds_ShouldReturnInsufficientFundsException()
	{
		Account account = AccountFactory.Create(balance: 100m).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>();
	}

	[Test]
	public async Task Debit_WithExchangeRateCausingInsufficientFunds_ShouldReturnInsufficientFundsException()
	{
		Account account = AccountFactory.Create(balance: 1000m).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 90m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>();
	}

	[Test]
	public async Task DebitTransfer_OnArchivedAccount_ShouldReturnArchivedAccountOperationException()
	{
		Account account = AccountFactory.Create(balance: 5000m).Value!;
		account.Archive(occurredAt: Now);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.DebitTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 1000m,
			forexRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task DebitTransfer_WithInsufficientFunds_ShouldReturnInsufficientFundsException()
	{
		Account account = AccountFactory.Create(balance: 100m).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.DebitTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 500m,
			forexRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InsufficientFundsException>();
	}

	[Test]
	public async Task DebitTransfer_WithInvalidForexRate_ShouldReturnInvalidExchangeRateException()
	{
		Account account = AccountFactory.Create(balance: 1000m).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.DebitTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 500m,
			forexRate: 0m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidExchangeRateException>();
	}

	[Test]
	public async Task CreditTransfer_OnArchivedAccount_ShouldReturnArchivedAccountOperationException()
	{
		Account account = AccountFactory.Create(balance: 0m).Value!;
		account.Archive(occurredAt: Now);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.CreditTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			fromAccountId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task RefundTransfer_ShouldIncreaseBalanceAndRaiseAccountTransferRefundedEvent()
	{
		Account account = AccountFactory.Create(balance: 500m).Value!;
		account.ClearEvents();

		account.RefundTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			amount: 1000m,
			description: "Refund: ToAccount not found."
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 1500m);
		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountTransferRefunded>();
	}

	[Test]
	public async Task Reconstitute_FromSnapshotAndEvents_ShouldRestoreCorrectState()
	{
		Account original = AccountFactory.Create(balance: 10000m).Value!;

		original.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 2000m,
			exchangeRate: 1m,
			description: null
		);

		string snapshotJson = Serializer.Serialize(aggregate: original);
		SnapshotData snapshot = new SnapshotData(
			AggregateId: original.Id,
			AggregateType: AggregateTypeNames.Account,
			Version: original.Version,
			State: snapshotJson
		);

		original.Credit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);

		IReadOnlyList<IEvent> postSnapshotEvents = original.Events
			.Where(predicate: e => e is AccountCredited)
			.ToList()
			.AsReadOnly();

		Account reconstituted = Account.Reconstitute(
			snapshot: snapshot,
			events: postSnapshotEvents,
			serializer: Serializer
		);

		await Assert.That(value: reconstituted.Id).IsEqualTo(expected: original.Id);
		await Assert.That(value: reconstituted.Balance.Amount).IsEqualTo(expected: 8500m);
		await Assert.That(value: reconstituted.UserId).IsEqualTo(expected: original.UserId);
		await Assert.That(value: reconstituted.IsArchived).IsFalse();
	}

	[Test]
	public async Task Reconstitute_FromSnapshotOnly_ShouldPreserveAllState()
	{
		Guid userId = Guid.CreateVersion7();
		Account account = AccountFactory.Create(userId: userId, balance: 9999m).Value!;
		account.Archive(occurredAt: Now);

		string snapshotJson = Serializer.Serialize(aggregate: account);
		SnapshotData snapshot = new SnapshotData(
			AggregateId: account.Id,
			AggregateType: AggregateTypeNames.Account,
			Version: account.Version,
			State: snapshotJson
		);

		Account restored = Account.Reconstitute(
			snapshot: snapshot,
			events: [],
			serializer: Serializer
		);

		await Assert.That(value: restored.Id).IsEqualTo(expected: account.Id);
		await Assert.That(value: restored.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: restored.Balance.Amount).IsEqualTo(expected: 9999m);
		await Assert.That(value: restored.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: restored.IsArchived).IsTrue();
		await Assert.That(value: restored.Version).IsEqualTo(expected: account.Version);
	}

	[Test]
	public async Task RefundTransfer_OnArchivedAccount_ShouldReturnArchivedAccountOperationException()
	{
		Account account = AccountFactory.Create(balance: 500m).Value!;
		account.Archive(occurredAt: Now);
		account.ClearEvents();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.RefundTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			amount: 500m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task RefundTransfer_WithZeroAmount_ShouldReturnInvalidAmountException()
	{
		Account account = AccountFactory.Create(balance: 500m).Value!;
		account.ClearEvents();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.RefundTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			amount: 0m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task RefundTransfer_WithNegativeAmount_ShouldReturnInvalidAmountException()
	{
		Account account = AccountFactory.Create(balance: 500m).Value!;
		account.ClearEvents();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.RefundTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			amount: -100m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task Credit_WithZeroAmount_ShouldReturnInvalidAmountException()
	{
		Account account = AccountFactory.Create(balance: 1000m).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Credit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 0m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task DebitTransfer_WithZeroAmount_ShouldReturnInvalidAmountException()
	{
		Account account = AccountFactory.Create(balance: 1000m).Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.DebitTransfer(
			occurredAt: Now,
			transferId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 0m,
			forexRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task Debit_WithExactBalance_ShouldSucceedAndLeaveZeroBalance()
	{
		Account account = AccountFactory.Create(balance: 1000m).Value!;
		account.ClearEvents();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 1m,
			description: null
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 0m);
	}
}
