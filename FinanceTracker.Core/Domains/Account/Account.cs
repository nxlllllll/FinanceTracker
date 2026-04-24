using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.Account;

public sealed class Account : AggregateRoot
{
	private sealed record AccountSnapshotState(
		Guid Id,
		Guid UserId,
		string Name,
		AccountType Type,
		string Currency,
		decimal Balance,
		bool IsArchived,
		int Version
	);
	
	public Guid UserId { get; private set; }
	public string Name { get; private set; } = String.Empty;
	public AccountType Type { get; private set; }
	public string Currency { get; private set; } = String.Empty;
	public decimal Balance { get; private set; }
	public bool IsArchived { get; private set; }

	private Account() { }

	public static Account Create(
		Guid userId,
		string name,
		AccountType type,
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
			Type: type,
			Currency: currency,
			Balance: balance,
			OccurredAt: DateTime.UtcNow
		));

		return account;
	}

	private static int GetSign(DirectionType direction)
	{
		int sign = direction switch
		{
			DirectionType.Credit => 1,
			DirectionType.Debit => -1,
			_ => throw new ArgumentOutOfRangeException(message: "Unknown direction type.", paramName: nameof(direction))
		};
		return sign;
	}
	
	public static Account ReconstituteFromHistory(IReadOnlyList<IEvent> history)
	{
		Account account = new Account();
		account.LoadEventsFromHistory(history: history);
		return account;
	}
	
	public static Account Restore(SnapshotData snapshot)
	{
		AccountSnapshotState state = System.Text.Json.JsonSerializer.Deserialize<AccountSnapshotState>(json: snapshot.State)!;

		Account account = new Account();
		account.Id = state.Id;
		account.UserId = state.UserId;
		account.Name = state.Name;
		account.Type = state.Type;
		account.Currency = state.Currency;
		account.Balance = state.Balance;
		account.IsArchived = state.IsArchived;
		account.RestoreVersion(version: state.Version);
		return account;
	}

	private void Apply(AccountBalanceAdjusted @event)
		=> Balance += @event.Delta;
	
	private void Apply(AccountDebited @event)
		=> Balance -= @event.Amount * @event.ExchangeRate;

	private void Apply(AccountCredited @event)
		=> Balance += @event.Amount * @event.ExchangeRate;

	private void Apply(AccountCreated @event)
	{
		Id = @event.AccountId;
		UserId = @event.UserId;
		Name = @event.Name;
		Type = @event.Type;
		Currency = @event.Currency;
		Balance = @event.Balance;
		IsArchived = false;
	}

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case AccountCreated e: Apply(@event: e); break;
			case AccountDebited e: Apply(@event: e); break;
			case AccountCredited e: Apply(@event: e); break;
			case AccountBalanceAdjusted e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public bool AdjustBalance(
		Guid sourceId,
		string sourceType,
		DirectionType direction,
		decimal oldRate,
		decimal newRate,
		decimal amount)
	{
		int sign = GetSign(direction: direction);
		decimal delta = (newRate - oldRate) * amount * sign;
		
		if (delta == 0)
			return false;
		
		RaiseEvent(@event: new AccountBalanceAdjusted(
			Id: Guid.NewGuid(),
			AccountId: Id,
			SourceId: sourceId,
			SourceType: sourceType,
			OldRate: oldRate,
			NewRate: newRate,
			Amount: amount,
			Delta: delta,
			OccurredAt: DateTime.UtcNow
		));
		return true;
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
	
	public string TakeSnapshot()
	{
		return System.Text.Json.JsonSerializer.Serialize(new AccountSnapshotState(
			Id: Id,
			UserId: UserId,
			Name: Name,
			Type: Type,
			Currency: Currency,
			Balance: Balance,
			IsArchived: IsArchived,
			Version: Version
		));
	}
}