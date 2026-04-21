using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.Account;

public sealed class Account : AggregateRoot
{
	public Guid UserId { get; private set; }
	public string Name { get; private set; } = String.Empty;
	public string AccountType { get; private set; } = String.Empty;
	public string Currency { get; private set; } = String.Empty;
	public decimal Balance { get; private set; }
	public bool IsArchived { get; private set; }

	private Account() { }

	public static Account Create(
		Guid userId,
		string name,
		string accountType,
		string currency,
		decimal balance)
	{
		if (String.IsNullOrWhiteSpace(value: name))
			throw new EmptyNameException(message: "The account name cannot be empty.");

		if (balance < 0)
			throw new InvalidInitialBalanceException(message: "The initial account balance cannot be negative.");

		Account account = new Account();
		account.RaiseEvent(@event: new AccountCreated(
			Id: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			UserId: userId,
			Name: name,
			AccountType: accountType,
			Currency: currency,
			Balance: balance,
			OccurredAt: DateTime.UtcNow
		));

		return account;
	}

	public static Account ReconstituteFromHistory(IReadOnlyList<IEvent> history)
	{
		Account account = new Account();
		account.LoadEventsFromHistory(history: history);
		return account;
	}

	private void Apply(AccountCreated @event)
	{
		Id = @event.AccountId;
		UserId = @event.UserId;
		Name = @event.Name;
		AccountType = @event.AccountType;
		Currency = @event.Currency;
		Balance = @event.Balance;
		IsArchived = false;
	}

	private void Apply(AccountRenamed @event)
		=> Name = @event.NewName;

	private void Apply(AccountArchived @event)
		=> IsArchived = true;

	private void Apply(AccountUnarchived @event)
		=> IsArchived = false;

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case AccountCreated e: Apply(@event: e); break;
			case AccountRenamed e: Apply(@event: e); break;
			case AccountArchived e: Apply(@event: e); break;
			case AccountUnarchived e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public void Rename(string newName)
	{
		if (String.IsNullOrWhiteSpace(value: newName))
			throw new EmptyNameException(message: "The account name cannot be empty.");

		if (Name.Equals(value: newName))
			return;

		RaiseEvent(new AccountRenamed(
			Id: Guid.NewGuid(),
			AccountId: Id,
			NewName: newName,
			OccurredAt: DateTime.UtcNow
		));
	}

	public void Archive()
	{
		if (IsArchived)
			throw new ArchivingException(message: "The account has already been archived before.");

		RaiseEvent(new AccountArchived(
			Id: Guid.NewGuid(),
			AccountId: Id,
			OccurredAt: DateTime.UtcNow
		));
	}

	public void Unarchive()
	{
		if (!IsArchived)
			throw new UnarchivingException(message: "The account is already active.");

		RaiseEvent(new AccountUnarchived(
			Id: Guid.NewGuid(),
			AccountId: Id,
			OccurredAt: DateTime.UtcNow
		));
	}
}