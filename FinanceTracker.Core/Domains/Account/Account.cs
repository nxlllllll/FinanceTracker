using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account;

public sealed class Account : AggregateRoot
{
	private sealed record AccountSnapshotState(
		Guid Id,
		Guid UserId,
		string Name,
		AccountType Type,
		Money Balance,
		bool IsArchived,
		int Version
	);
	
	public Guid UserId { get; private set; }
	public string Name { get; private set; } = String.Empty;
	public AccountType Type { get; private set; }
	public Money Balance { get; private set; }
	public Currency Currency => Balance.Currency;
	public bool IsArchived { get; private set; }

	private Account() { }

	private static int GetSign(DirectionType direction)
	{
		int sign = direction switch
		{
			DirectionType.Credit => 1,
			DirectionType.Debit => -1,
			_ => throw new InvalidTransactionDirectionException(message: "Unknown direction type.")
		};
		return sign;
	}
	
	public static Result<Account, DomainException> Create(
		DateTime occurredAt,
		Guid userId,
		string name,
		AccountType type,
		Currency currency,
		decimal balance)
	{
		if (String.IsNullOrWhiteSpace(value: name))
			return Result<Account, DomainException>.Failure(error: new NameException(message: "The account name cannot be empty."));
 
		if (balance < 0)
			return Result<Account, DomainException>.Failure(error: new InvalidInitialBalanceException(message: "The initial account balance cannot be negative."));
 
		Account account = new Account();
		account.RaiseEvent(@event: new AccountCreated(
			Id: Guid.NewGuid(),
			AccountId: Guid.NewGuid(),
			UserId: userId,
			Name: name,
			Type: type,
			Currency: currency,
			Balance: balance,
			OccurredAt: occurredAt
		));
 
		return Result<Account, DomainException>.Success(value: account);
	}
	
	public static Account ReconstituteFromHistory(IReadOnlyList<IEvent> history)
	{
		Account account = new Account();
		account.LoadEventsFromHistory(history: history);
		return account;
	}
	
	public static Account Reconstitute(
		SnapshotData? snapshot,
		IReadOnlyList<IEvent> events)
	{
		Account account = snapshot is null ? new Account() : Restore(snapshot: snapshot);
		account.LoadEventsFromHistory(events);
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
		account.Balance = state.Balance;
		account.IsArchived = state.IsArchived;
		account.RestoreVersion(version: state.Version);
		return account;
	}

	private Result<Unit, DomainException> CheckConstraints(decimal amount, decimal rate = 1m)
	{
		if (IsArchived)
			return Result<Unit, DomainException>.Failure(error: new ArchivedAccountOperationException(message: "Financial transactions on the archived account are prohibited."));
 
		if (amount <= 0)
			return Result<Unit, DomainException>.Failure(error: new InvalidAmountException(message: "Amount must be greater than zero."));
 
		if (rate <= 0)
			return Result<Unit, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
	
	private Result<Unit, DomainException> CheckSufficientFunds(decimal amount, decimal rate = 1m)
	{
		if (amount * rate > Balance.Amount)
			return Result<Unit, DomainException>.Failure(error: new InsufficientFundsException("The amount of funds on the balance is insufficient.", balance: Balance));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	private void Apply(AccountBalanceAdjusted @event)
		=> Balance += @event.Delta;
	
	private void Apply(AccountDebited @event)
		=> Balance -= @event.Amount * @event.ExchangeRate;

	private void Apply(AccountCredited @event)
		=> Balance += @event.Amount * @event.ExchangeRate;
	
	private void Apply(AccountTransferDebited @event)
		=> Balance -= @event.Amount;

	private void Apply(AccountTransferCredited @event)
		=> Balance += @event.Amount * @event.ExchangeRate;

	private void Apply(AccountRenamed @event)
		=> Name = @event.NewName;
	
	private void Apply(AccountArchived @event)
		=> IsArchived = true;
	
	private void Apply(AccountUnarchived @event)
		=> IsArchived = false;
	
	private void Apply(AccountCreated @event)
	{
		Id = @event.AccountId;
		UserId = @event.UserId;
		Name = @event.Name;
		Type = @event.Type;
		Balance = Money.Create(amount: @event.Balance, currency: Currency.Create(value: @event.Currency).Value).Value;
		IsArchived = false;
	}

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case AccountCreated e: Apply(@event: e); break;
			case AccountRenamed e: Apply(@event: e); break;
			case AccountArchived e: Apply(@event: e); break;
			case AccountUnarchived e: Apply(@event: e); break;
			case AccountDebited e: Apply(@event: e); break;
			case AccountCredited e: Apply(@event: e); break;
			case AccountBalanceAdjusted e: Apply(@event: e); break;
			case AccountTransferDebited e: Apply(@event: e); break;
			case AccountTransferCredited e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public Result<Unit, DomainException> AdjustBalance(
		DateTime occurredAt,
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
			return Result<Unit, DomainException>.Success(value: Unit.Default);
 
		RaiseEvent(@event: new AccountBalanceAdjusted(
			Id: Guid.NewGuid(),
			AccountId: Id,
			SourceId: sourceId,
			SourceType: sourceType,
			OldRate: oldRate,
			NewRate: newRate,
			Amount: amount,
			Delta: delta,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Debit(
		DateTime occurredAt,
		Guid transactionId,
		Guid categoryId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount, rate: exchangeRate);
		if (constraints.IsFailure) return constraints;
 
		Result<Unit, DomainException> funds = CheckSufficientFunds(amount: amount, rate: exchangeRate);
		if (funds.IsFailure) return funds;
 
		RaiseEvent(@event: new AccountDebited(
			Id: Guid.NewGuid(),
			AccountId: Id,
			TransactionId: transactionId,
			CategoryId: categoryId,
			Amount: amount,
			ExchangeRate: exchangeRate,
			Description: description,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> DebitTransfer(
		DateTime occurredAt,
		Guid transferId,
		Guid toAccountId,
		decimal amount,
		decimal forexRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount);
		if (constraints.IsFailure) return constraints;
 
		Result<Unit, DomainException> funds = CheckSufficientFunds(amount: amount);
		if (funds.IsFailure) return funds;
 
		RaiseEvent(@event: new AccountTransferDebited(
			Id: Guid.NewGuid(),
			AccountId: Id,
			TransferId: transferId,
			ToAccountId: toAccountId,
			Amount: amount,
			ForexRate: forexRate,
			Description: description,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Credit(
		DateTime occurredAt,
		Guid transactionId,
		Guid categoryId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount, rate: exchangeRate);
		if (constraints.IsFailure) return constraints;
 
		RaiseEvent(@event: new AccountCredited(
			Id: Guid.NewGuid(),
			AccountId: Id,
			TransactionId: transactionId,
			CategoryId: categoryId,
			Amount: amount,
			ExchangeRate: exchangeRate,
			Description: description,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> CreditTransfer(
		DateTime occurredAt,
		Guid transferId,
		Guid fromAccountId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount, rate: exchangeRate);
		if (constraints.IsFailure) return constraints;
 
		RaiseEvent(@event: new AccountTransferCredited(
			Id: Guid.NewGuid(),
			AccountId: Id,
			TransferId: transferId,
			FromAccountId: fromAccountId,
			Amount: amount,
			ExchangeRate: exchangeRate,
			Description: description,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Rename(
		DateTime occurredAt,
		string newName)
	{
		if (String.IsNullOrWhiteSpace(value: newName))
			return Result<Unit, DomainException>.Failure(error: new NameException(message: "The account name cannot be empty."));
 
		if (Name.Equals(value: newName, comparisonType: StringComparison.OrdinalIgnoreCase))
			return Result<Unit, DomainException>.Success(value: Unit.Default);
 
		RaiseEvent(@event: new AccountRenamed(
			Id: Guid.NewGuid(),
			AccountId: Id,
			NewName: newName,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Archive(DateTime occurredAt)
	{
		if (IsArchived)
			return Result<Unit, DomainException>.Failure(error: new ArchivingException(message: "The account has already been archived before."));
 
		RaiseEvent(@event: new AccountArchived(
			Id: Guid.NewGuid(),
			AccountId: Id,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Unarchive(DateTime occurredAt)
	{
		if (!IsArchived)
			return Result<Unit, DomainException>.Failure(error: new UnarchivingException(message: "The account is already active."));
 
		RaiseEvent(@event: new AccountUnarchived(
			Id: Guid.NewGuid(),
			AccountId: Id,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
	
	public string TakeSnapshot()
	{
		return System.Text.Json.JsonSerializer.Serialize(value: new AccountSnapshotState(
			Id: Id,
			UserId: UserId,
			Name: Name,
			Type: Type,
			Balance: Balance,
			IsArchived: IsArchived,
			Version: Version
		));
	}
}