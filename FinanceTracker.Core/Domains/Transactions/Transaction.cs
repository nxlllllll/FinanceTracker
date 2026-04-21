using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Transactions.Events;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.Transactions;

public sealed class Transaction : AggregateRoot
{
	public Guid AccountId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid CategoryId { get; private set; }
	public decimal Amount { get; private set; }
	public DirectionType Direction { get; private set; }
	public decimal ExchangeRate { get; private set; }
	public string? Description { get; private set; }
	public bool IsExcluded { get; private set; }
	public DateTime OccurredAt { get; private set; }

	private Transaction() { }

	public static Transaction Create(
		Guid accountId,
		Guid userId,
		Guid categoryId,
		decimal amount,
		DirectionType direction,
		decimal exchangeRate,
		string? description,
		DateTime occurredAt)
	{
		if (amount <= 0)
			throw new InvalidAmountException(message: "The transaction amount cannot be less than or equal to zero.");

		if (exchangeRate <= 0)
			throw new InvalidExchangeRateException(message: "The exchange rate cannot be less than or equal to zero.");

		Transaction transaction = new Transaction();
		transaction.RaiseEvent(@event: new TransactionCreated(
			Id: Guid.NewGuid(),
			TransactionId: Guid.NewGuid(),
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: amount,
			Direction: direction,
			ExchangeRate: exchangeRate,
			Description: description,
			OccurredAt: occurredAt
		));

		return transaction;
	}

	public static Transaction Reconstitute(
		Guid id,
		Guid accountId,
		Guid userId,
		Guid categoryId,
		decimal amount,
		DirectionType directionType,
		decimal exchangeRate,
		string? description,
		bool isExcluded,
		DateTime occurredAt)
	{
		return new Transaction()
		{
			Id = id,
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Direction = directionType,
			ExchangeRate = exchangeRate,
			Description = description,
			IsExcluded = isExcluded,
			OccurredAt = occurredAt
		};
	}

	public static Transaction ReconstituteFromHistory(IReadOnlyList<IEvent> history)
	{
		Transaction transaction = new Transaction();
		transaction.LoadEventsFromHistory(history: history);
		return transaction;
	}

	private void Apply(TransactionCreated @event)
	{
		Id = @event.TransactionId;
		AccountId = @event.AccountId;
		UserId = @event.UserId;
		CategoryId = @event.CategoryId;
		Amount = @event.Amount;
		Direction = @event.Direction;
		ExchangeRate = @event.ExchangeRate;
		Description = @event.Description;
		IsExcluded = false;
		OccurredAt = @event.OccurredAt;
	}

	private void Apply(TransactionCategoryChanged @event)
		=> CategoryId = @event.CategoryId;

	private void Apply(TransactionDescriptionChanged @event)
		=> Description = @event.Description;

	private void Apply(TransactionIncluded @event)
		=> IsExcluded = false;

	private void Apply(TransactionExcluded @event)
		=> IsExcluded = true;

	protected override void Apply(IEvent @event)
	{
		switch (@event)
		{
			case TransactionCreated e: Apply(@event: e); break;
			case TransactionCategoryChanged e: Apply(@event: e); break;
			case TransactionDescriptionChanged e: Apply(@event: e); break;
			case TransactionIncluded e: Apply(@event: e); break;
			case TransactionExcluded e: Apply(@event: e); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	public void ChangeCategory(Guid categoryId)
	{
		if (CategoryId == categoryId)
			return;

		RaiseEvent(@event: new TransactionCategoryChanged(
			Id: Guid.NewGuid(),
			TransactionId: Id,
			CategoryId: categoryId,
			OccurredAt: DateTime.UtcNow
		));
	}

	public void ChangeDescription(string description)
	{
		if (Description == description)
			return;

		RaiseEvent(@event: new TransactionDescriptionChanged(
			Id: Guid.NewGuid(),
			TransactionId: Id,
			Description: description,
			OccurredAt: OccurredAt
		));
	}

	public void Include()
	{
		if (!IsExcluded)
			throw new IncludingException(message: "The transaction is already included.");

		RaiseEvent(new TransactionIncluded(
			Id: Guid.NewGuid(),
			TransactionId: Id,
			OccurredAt: DateTime.UtcNow
		));
	}

	public void Exclude()
	{
		if (IsExcluded)
			throw new ExcludingException(message: "The transaction is already excluded.");

		RaiseEvent(new TransactionExcluded(
			Id: Guid.NewGuid(),
			TransactionId: Id,
			OccurredAt: DateTime.UtcNow
		));
	}
}