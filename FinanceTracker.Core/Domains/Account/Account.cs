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
	
	private void Apply(AccountDebited @event)
		=> Balance -= @event.Amount * @event.ExchangeRate;

	private void Apply(AccountCredited @event)
		=> Balance += @event.Amount * @event.ExchangeRate;

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case AccountCreated e: Apply(@event: e); break;
			case AccountDebited e: Apply(@event: e); break;
			case AccountCredited e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public void Debit(
		Guid transactionId,
		Guid categoryId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		if (IsArchived)
			throw new ArchivingException(message: "Cannot debit an archived account.");

		if (amount <= 0)
			throw new InvalidAmountException(message: "Amount must be greater than zero.");

		if (exchangeRate <= 0)
			throw new InvalidExchangeRateException(message: "Exchange rate must be greater than zero.");

		RaiseEvent(@event: new AccountDebited(
			Id: Guid.NewGuid(),
			AccountId: Id,
			TransactionId: transactionId,
			CategoryId: categoryId,
			Amount: amount,
			ExchangeRate: exchangeRate,
			Description: description,
			OccurredAt: DateTime.UtcNow
		));
	}

	public void Credit(
		Guid transactionId,
		Guid categoryId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		if (IsArchived)
			throw new ArchivingException(message: "Cannot credit an archived account.");

		if (amount <= 0)
			throw new InvalidAmountException(message: "Amount must be greater than zero.");

		if (exchangeRate <= 0)
			throw new InvalidExchangeRateException(message: "Exchange rate must be greater than zero.");

		RaiseEvent(@event: new AccountCredited(
			Id: Guid.NewGuid(),
			AccountId: Id,
			TransactionId: transactionId,
			CategoryId: categoryId,
			Amount: amount,
			ExchangeRate: exchangeRate,
			Description: description,
			OccurredAt: DateTime.UtcNow
		));
	}
	
	public bool Rename(string newName)
	{
		if (String.IsNullOrWhiteSpace(value: newName))
			throw new EmptyNameException(message: "The account name cannot be empty.");

		if (Name.Equals(value: newName))
			return false;

		Name = newName;
		return true;
	}

	public bool Archive()
	{
		if (IsArchived)
			throw new ArchivingException(message: "The account has already been archived before.");

		IsArchived = true;
		return true;
	}

	public bool Unarchive()
	{
		if (!IsArchived)
			throw new UnarchivingException(message: "The account is already active.");

		IsArchived = false;
		return true;
	}
}