using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class AccountTests
{
	#region Creation

	[Test]
	public async Task Create_WithValidData_ShouldRaiseAccountCreatedEvent()
	{
		Account account = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 10000
		);

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountCreated>();
	}

	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.NewGuid();

		Account account = Account.Create(
			userId: userId,
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 10000
		);

		await Assert.That(value: account.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: account.Name).IsEqualTo(expected: "Карта Сбер");
		await Assert.That(value: account.AccountType).IsEqualTo(expected: "checking");
		await Assert.That(value: account.Currency).IsEqualTo(expected: "RUB");
		await Assert.That(value: account.Balance).IsEqualTo(expected: 10000);
		await Assert.That(value: account.IsArchived).IsFalse();
		await Assert.That(value: account.Version).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Create_WithEmptyName_ShouldThrowArgumentException()
	{
		await Assert.That(func: () => Account.Create(
			userId: Guid.NewGuid(),
			name: String.Empty,
			accountType: "checking",
			currency: "RUB",
			balance: 10000
		)).Throws<EmptyNameException>();
	}

	[Test]
	public async Task Create_WithNegativeBalance_ShouldThrowArgumentException()
	{
		await Assert.That(func: () => Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: -100
		)).Throws<InvalidInitialBalanceException>();
	}

	#endregion

	#region Rename

	[Test]
	public async Task Rename_WithNewName_ShouldRaiseAccountRenamedEvent()
	{
		Account account = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);

		account.ClearEvents();
		account.Rename(newName: "Карта Тинькофф");

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountRenamed>();
		await Assert.That(value: account.Name).IsEqualTo(expected: "Карта Тинькофф");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldNotRaiseEvent()
	{
		Account account = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);

		account.ClearEvents();
		account.Rename(newName: "Карта Сбер");

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 0);
	}

	#endregion

	#region Archive

	[Test]
	public async Task Archive_ActiveAccount_ShouldRaiseAccountArchivedEvent()
	{
		Account account = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);

		account.ClearEvents();
		account.Archive();

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountArchived>();
		await Assert.That(value: account.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_AlreadyArchivedAccount_ShouldThrowArgumentException()
	{
		Account account = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);

		account.Archive();

		await Assert.That(action: account.Archive).Throws<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_ArchivedAccount_ShouldRaiseAccountUnarchivedEvent()
	{
		Account account = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);

		account.Archive();
		account.ClearEvents();
		account.Unarchive();

		await Assert.That(value: account.Events).Count().IsEqualTo(expected: 1);
		await Assert.That(value: account.Events[0]).IsTypeOf<AccountUnarchived>();
		await Assert.That(value: account.IsArchived).IsFalse();
	}

	#endregion

	#region ReconstituteFromHistory

	[Test]
	public async Task ReconstituteFromHistory_ShouldRestoreCorrectState()
	{
		Account original = Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 10000
		);

		original.Rename(newName: "Карта Тинькофф");
		original.Archive();

		Account reconstituted = Account.ReconstituteFromHistory(history: original.Events.ToList());

		await Assert.That(value: reconstituted.Id).IsEqualTo(expected: original.Id);
		await Assert.That(value: reconstituted.Name).IsEqualTo(expected: "Карта Тинькофф");
		await Assert.That(value: reconstituted.IsArchived).IsTrue();
		await Assert.That(value: reconstituted.Version).IsEqualTo(expected: original.Version);
		await Assert.That(value: reconstituted.Events).Count().IsEqualTo(expected: 0);
	}

	#endregion
}