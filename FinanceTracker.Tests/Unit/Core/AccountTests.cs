using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class AccountTests
{
	[Test]
	public async Task Create_WithValidData_ShouldRaiseAccountCreatedEvent()
	{
		Account account = AccountFactory.Create();

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountCreated>();
	}

	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.NewGuid();

		Account account = AccountFactory.Create(userId: userId, balance: 10000);

		await Assert.That(value: account.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: account.Name).IsEqualTo(expected: "Карта Сбер");
		await Assert.That(value: account.Type).IsEqualTo(expected: AccountType.Checking);
		await Assert.That(value: account.Currency).IsEqualTo(expected: "RUB");
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 10000);
		await Assert.That(value: account.IsArchived).IsFalse();
		await Assert.That(value: account.Version).IsEqualTo(expected: 1);
	}
	
	[Test]
	public async Task Create_WithEmptyName_ShouldThrowEmptyNameException()
		=> await Assert.That(func: () => AccountFactory.Create(name: String.Empty)).Throws<EmptyNameException>();

	[Test]
	public async Task Create_WithNegativeBalance_ShouldThrowInvalidInitialBalanceException()
		=> await Assert.That(func: () => AccountFactory.Create(balance: -100)).Throws<InvalidInitialBalanceException>();

	[Test]
	public async Task Rename_WithNewName_ShouldChangeName()
	{
		Account account = AccountFactory.Create();

		account.Rename(newName: "Карта Тинькофф");

		await Assert.That(value: account.Name).IsEqualTo(expected: "Карта Тинькофф");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldReturnFalse()
	{
		Account account = AccountFactory.Create(name: "Карта Сбер");
		account.ClearEvents();
		
		account.Rename(newName: "Карта Сбер");

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Rename_WithEmptyName_ShouldThrowEmptyNameException()
	{
		Account account = AccountFactory.Create();
		
		await Assert.That(action: () => account.Rename(newName: String.Empty)).Throws<EmptyNameException>();
	}
	
	[Test]
	public async Task Archive_ActiveAccount_ShouldReturnTrueAndSetIsArchived()
	{
		Account account = AccountFactory.Create();

		account.Archive();

		await Assert.That(value: account.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_AlreadyArchivedAccount_ShouldThrowArchivingException()
	{
		Account account = AccountFactory.Create();

		account.Archive();

		await Assert.That(action: account.Archive).Throws<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_ArchivedAccount_ShouldReturnTrueAndClearIsArchived()
	{
		Account account = AccountFactory.Create();

		account.Archive();
		account.Unarchive();

		await Assert.That(value: account.IsArchived).IsFalse();
	}

	[Test]
	public async Task Unarchive_ActiveAccount_ShouldThrowUnarchivingException()
	{
		Account account = AccountFactory.Create();

		await Assert.That(action: account.Unarchive).Throws<UnarchivingException>();
	}
	
	[Test]
    public async Task Debit_WithValidData_ShouldRaiseAccountDebitedEvent()
	{
		Account account = AccountFactory.Create(balance: 10000);
        account.ClearEvents();

        account.Debit(
            transactionId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: 1000m,
            exchangeRate: 1m,
            description: "Обед"
        );

        await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
        await Assert.That(value: account.Events[0]).IsTypeOf<AccountDebited>();
        await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 9000m);
    }

    [Test]
    public async Task Debit_WithExchangeRate_ShouldApplyExchangeRate()
    {
		Account account = AccountFactory.Create(balance: 10000);
        account.ClearEvents();

        account.Debit(
            transactionId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: 100m,
            exchangeRate: 90m,
            description: null
        );

        await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 1000m);
    }

    [Test]
    public async Task Debit_OnArchivedAccount_ShouldThrowArchivingException()
    {
		Account account = AccountFactory.Create();
        account.Archive();

        await Assert.That(action: () => account.Debit(
            transactionId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: 100m,
            exchangeRate: 1m,
            description: null
        )).Throws<ArchivingException>();
    }

    [Test]
    public async Task Debit_WithZeroAmount_ShouldThrowInvalidAmountException()
    {
		Account account = AccountFactory.Create(balance: 10000);
		
        await Assert.That(action: () => account.Debit(
            transactionId: Guid.NewGuid(),
            categoryId: Guid.NewGuid(),
            amount: 0m,
            exchangeRate: 1m,
            description: null
        )).Throws<InvalidAmountException>();
    }
	
	[Test]
	public async Task Credit_WithValidData_ShouldRaiseAccountCreditedEvent()
	{
		Account account = AccountFactory.Create(balance: 1000);
		account.ClearEvents();

		account.Credit(
			transactionId: Guid.NewGuid(),
			categoryId: Guid.NewGuid(),
			amount: 500m,
			exchangeRate: 1m,
			description: "Зарплата"
		);

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountCredited>();
		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 1500m);
	}

	[Test]
	public async Task Credit_OnArchivedAccount_ShouldThrowArchivingException()
	{
		Account account = AccountFactory.Create(balance: 0);
		account.Archive();

		await Assert.That(action: () => account.Credit(
			transactionId: Guid.NewGuid(),
			categoryId: Guid.NewGuid(),
			amount: 100m,
			exchangeRate: 1m,
			description: null
		)).Throws<ArchivingException>();
	}
	
	[Test]
	public async Task ReconstituteFromHistory_ShouldRestoreCorrectState()
	{
		Account original = AccountFactory.Create(balance: 10000);

		original.Debit(
			transactionId: Guid.NewGuid(),
			categoryId: Guid.NewGuid(),
			amount: 1000m,
			exchangeRate: 1m,
			description: null
		);

		original.Credit(
			transactionId: Guid.NewGuid(),
			categoryId: Guid.NewGuid(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);

		Account reconstituted = Account.ReconstituteFromHistory(history: original.Events.ToList());

		await Assert.That(value: reconstituted.Id).IsEqualTo(expected: original.Id);
		await Assert.That(value: reconstituted.Balance.Amount).IsEqualTo(expected: 9500m);
		await Assert.That(value: reconstituted.Version).IsEqualTo(expected: original.Version);
		await Assert.That(value: reconstituted.Events).Count().IsEqualTo(expected: 0);
	}
	
	[Test]
	public async Task AdjustBalance_WithDebitAndRateIncrease_ShouldDecreaseBalance()
	{
		Account account = AccountFactory.Create(balance: 10000);
	    account.ClearEvents();

	    account.AdjustBalance(
	        sourceId: Guid.NewGuid(),
	        sourceType: "Transaction",
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
		Account account = AccountFactory.Create(balance: 10000);
		account.ClearEvents();

	    account.AdjustBalance(
	        sourceId: Guid.NewGuid(),
	        sourceType: "Transaction",
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
		Account account = AccountFactory.Create(balance: 10000);
		account.ClearEvents();

	    account.AdjustBalance(
	        sourceId: Guid.NewGuid(),
	        sourceType: "Transaction",
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
		Account account = AccountFactory.Create(balance: 10000);
	    account.ClearEvents();

	    account.AdjustBalance(
	        sourceId: Guid.NewGuid(),
	        sourceType: "Transaction",
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
		Account account = AccountFactory.Create(balance: 10000m);
		account.ClearEvents();

		account.DebitTransfer(
			transferId: Guid.NewGuid(),
			toAccountId: Guid.NewGuid(),
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
		Account account = AccountFactory.Create(balance: 0m);
		account.ClearEvents();

		account.CreditTransfer(
			transferId: Guid.NewGuid(),
			fromAccountId: Guid.NewGuid(),
			amount: 1000m,
			exchangeRate: 0.011m,
			description: null
		);

		await Assert.That(value: account.Balance.Amount).IsEqualTo(expected: 11m);
		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountTransferCredited>();
	}
}