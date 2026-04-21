using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Domains.Transactions.Events;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Projections;

public sealed class TransactionProjection(
	ITransactionWriteRepository transactionWriteRepository,
	IAccountWriteRepository accountWriteRepository
) : INotificationHandler<TransactionEventsNotification>
{
	private async Task HandleAsync(IEvent @event, CancellationToken ct = default)
	{
		switch (@event)
		{
			case TransactionCreated e: await HandleAsync(@event: e, ct: ct); break;
			case TransactionCategoryChanged e: await HandleAsync(@event: e, ct: ct); break;
			case TransactionDescriptionChanged e: await HandleAsync(@event: e, ct: ct); break;
			case TransactionIncluded e: await HandleAsync(@event: e, ct: ct); break;
			case TransactionExcluded e: await HandleAsync(@event: e, ct: ct); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}
	
	private async Task UpdateBalanceAsync(
		Guid accountId,
		decimal amount,
		DirectionType direction,
		decimal exchangeRate,
		CancellationToken ct = default)
	{
		decimal delta = direction switch
		{
			DirectionType.Credit => amount * exchangeRate,
			DirectionType.Debit => -amount * exchangeRate,
			_ => throw new ArgumentOutOfRangeException(message: "Unknown type of direction for the transaction.", paramName: nameof(direction))
		};

		await accountWriteRepository.UpdateBalanceAsync(
			accountId: accountId,
			amount: delta,
			ct: ct
		);
	}
	
	private async Task HandleAsync(TransactionCreated @event, CancellationToken ct = default)
	{
		await transactionWriteRepository.CreateAsync(@event: @event, ct: ct);
		await UpdateBalanceAsync(
			accountId: @event.AccountId,
			amount: @event.Amount,
			direction: @event.Direction,
			exchangeRate: @event.ExchangeRate,
			ct: ct
		);
	}
	
	private async Task HandleAsync(TransactionCategoryChanged @event, CancellationToken ct = default)
		=> await transactionWriteRepository.ChangeCategoryAsync(@event: @event, ct: ct);

	private async Task HandleAsync(TransactionDescriptionChanged @event, CancellationToken ct = default)
		=> await transactionWriteRepository.ChangeDescriptionAsync(@event: @event, ct: ct);
	
	private async Task HandleAsync(TransactionIncluded @event, CancellationToken ct = default)
		=> await transactionWriteRepository.IncludeAsync(@event: @event, ct: ct);

	private async Task HandleAsync(TransactionExcluded @event, CancellationToken ct = default)
		=> await transactionWriteRepository.ExcludeAsync(@event: @event, ct: ct);
	
	public async Task Handle(
		TransactionEventsNotification notification,
		CancellationToken ct = default)
	{
		foreach (IEvent @event in notification.Events)
			await HandleAsync(@event: @event, ct: ct);
	}
}