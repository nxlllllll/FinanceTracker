using System.Text.Json.Serialization;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Account;

public sealed class Account : AggregateRoot
{
	private sealed record AccountSnapshotState(
		[property: JsonPropertyName("id")] Guid Id,
		[property: JsonPropertyName("user_id")] Guid UserId,
		[property: JsonPropertyName("name")] Name Name,
		[property: JsonPropertyName("type")] AccountType Type,
		[property: JsonPropertyName("balance")] Money Balance,
		[property: JsonPropertyName("is_archived")] bool IsArchived,
		[property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
		[property: JsonPropertyName("version")] int Version
	);

	public Guid UserId { get; private set; }
	public Name Name { get; private set; }
	public AccountType Type { get; private set; }
	public Money Balance { get; private set; }
	public Currency Currency => Balance.Currency;
	public bool IsArchived { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }

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
		DateTimeOffset occurredAt,
		Guid userId,
		Name name,
		AccountType type,
		Currency currency,
		decimal balance)
	{
		if (balance < 0)
			return Result<Account, DomainException>.Failure(error: new InvalidInitialBalanceException(message: "The initial account balance cannot be negative."));
 
		Account account = new Account();
		account.RaiseEvent(@event: new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: userId,
			Name: name,
			Type: type,
			Currency: currency,
			Balance: balance,
			OccurredAt: occurredAt
		));
 
		return Result<Account, DomainException>.Success(value: account);
	}
	
	public static Account Reconstitute(
		SnapshotData? snapshot,
		IReadOnlyList<IEvent> events)
	{
		Account account = snapshot is null ? new Account() : Restore(snapshot: snapshot);
		account.LoadEventsFromHistory(events);
		return account;
	}
	
	public static Account Reconstitute(
		Guid id,
		Guid userId,
		Name name,
		AccountType type,
		Money balance,
		bool isArchived,
		DateTimeOffset createdAt)
	{
		return new Account
		{
			Id = id,
			UserId = userId,
			Name = name,
			Type = type,
			Balance = balance,
			IsArchived = isArchived,
			CreatedAt = createdAt
		};
	}

	internal static Account Restore(SnapshotData snapshot)
	{
		AccountSnapshotState state = System.Text.Json.JsonSerializer.Deserialize<AccountSnapshotState>(
			json: snapshot.State,
			options: FinanceTrackerJsonOptions.Payload
		)!;

		Account account = new Account
		{
			Id = state.Id,
			UserId = state.UserId,
			Name = state.Name,
			Type = state.Type,
			Balance = state.Balance,
			IsArchived = state.IsArchived,
			CreatedAt = state.CreatedAt
		};
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
			return Result<Unit, DomainException>.Failure(error: new InsufficientFundsException(message: "The amount of funds on the balance is insufficient.", balance: Balance));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	private void Apply(AccountBalanceAdjusted @event)
		=> Balance = Balance.Add(amount: @event.Delta);
	
	private void Apply(AccountDebited @event)
		=> Balance = Balance.Subtract(amount: @event.Amount * @event.ExchangeRate);

	private void Apply(AccountCredited @event)
		=> Balance = Balance.Add(amount: @event.Amount * @event.ExchangeRate);
	
	private void Apply(AccountTransferDebited @event)
		=> Balance = Balance.Subtract(amount: @event.Amount);

	private void Apply(AccountTransferCredited @event)
		=> Balance = Balance.Add(amount: @event.Amount * @event.ExchangeRate);

	private void Apply(AccountTransferRefunded @event)
		=> Balance = Balance.Add(amount: @event.Amount);
	
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
		Balance = Money.Reconstitute(amount: @event.Balance, currency: @event.Currency);
		IsArchived = false;
		CreatedAt = @event.OccurredAt;
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
			case AccountTransferRefunded e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public Result<Unit, DomainException> AdjustBalance(
		DateTimeOffset occurredAt,
		Guid sourceId,
		string sourceType,
		DirectionType direction,
		decimal oldRate,
		decimal newRate,
		decimal amount)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount);
		if (constraints.IsFailure) 
			return constraints;
			
		int sign = GetSign(direction: direction);
		decimal delta = (newRate - oldRate) * amount * sign;
 
		if (delta == 0)
			return Result<Unit, DomainException>.Success(value: Unit.Default);
 
		RaiseEvent(@event: new AccountBalanceAdjusted(
			Id: Guid.CreateVersion7(),
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
		DateTimeOffset occurredAt,
		Guid transactionId,
		Guid categoryId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount, rate: exchangeRate);
		if (constraints.IsFailure) 
			return constraints;
 
		Result<Unit, DomainException> funds = CheckSufficientFunds(amount: amount, rate: exchangeRate);
		if (funds.IsFailure)
			return funds;
 
		RaiseEvent(@event: new AccountDebited(
			Id: Guid.CreateVersion7(),
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
		DateTimeOffset occurredAt,
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
			Id: Guid.CreateVersion7(),
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
 
	public Result<Unit, DomainException> RefundTransfer(
		DateTimeOffset occurredAt,
		Guid transferId,
		decimal amount,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount);
		if (constraints.IsFailure) return constraints;
		
		RaiseEvent(@event: new AccountTransferRefunded(
			Id: Guid.CreateVersion7(),
			AccountId: Id,
			TransferId: transferId,
			Amount: amount,
			Description: description,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
	
	public Result<Unit, DomainException> Credit(
		DateTimeOffset occurredAt,
		Guid transactionId,
		Guid categoryId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount, rate: exchangeRate);
		if (constraints.IsFailure) return constraints;
 
		RaiseEvent(@event: new AccountCredited(
			Id: Guid.CreateVersion7(),
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
		DateTimeOffset occurredAt,
		Guid transferId,
		Guid fromAccountId,
		decimal amount,
		decimal exchangeRate,
		string? description)
	{
		Result<Unit, DomainException> constraints = CheckConstraints(amount: amount, rate: exchangeRate);
		if (constraints.IsFailure) return constraints;
 
		RaiseEvent(@event: new AccountTransferCredited(
			Id: Guid.CreateVersion7(),
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
		DateTimeOffset occurredAt,
		Name newName)
	{
		if (Name == newName)
			return Result<Unit, DomainException>.Success(value: Unit.Default);
 
		RaiseEvent(@event: new AccountRenamed(
			Id: Guid.CreateVersion7(),
			AccountId: Id,
			NewName: newName,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Archive(DateTimeOffset occurredAt)
	{
		if (IsArchived)
			return Result<Unit, DomainException>.Failure(error: new ArchivingException(message: "The account has already been archived before."));
 
		RaiseEvent(@event: new AccountArchived(
			Id: Guid.CreateVersion7(),
			AccountId: Id,
			OccurredAt: occurredAt
		));
 
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Unarchive(DateTimeOffset occurredAt)
	{
		if (!IsArchived)
			return Result<Unit, DomainException>.Failure(error: new UnarchivingException(message: "The account is already active."));
 
		RaiseEvent(@event: new AccountUnarchived(
			Id: Guid.CreateVersion7(),
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
			CreatedAt: CreatedAt,
			Version: Version
		), options: FinanceTrackerJsonOptions.Payload);
	}
}