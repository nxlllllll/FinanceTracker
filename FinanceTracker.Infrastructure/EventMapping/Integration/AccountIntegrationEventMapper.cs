using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account.Events;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.EventMapping.Integration;

public sealed class AccountIntegrationEventMapper(ILogger<AccountIntegrationEventMapper> logger) : IIntegrationEventMapper
{
	public IAccountIntegrationEvent? Map(IEvent @event) => @event switch
	{
		AccountCreated e => new AccountCreatedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			UserId: e.UserId,
			Name: e.Name.Value,
			AccountType: e.Type.ToString(),
			Currency: e.Currency.Value,
			Balance: e.Balance,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountDebited e => new AccountDebitedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			TransactionId: e.TransactionId,
			CategoryId: e.CategoryId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountCredited e => new AccountCreditedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			TransactionId: e.TransactionId,
			CategoryId: e.CategoryId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountRenamed e => new AccountRenamedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			NewName: e.NewName.Value,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountArchived e => new AccountArchivedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountUnarchived e => new AccountUnarchivedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountTransferDebited e => new AccountTransferDebitedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			ToAccountId: e.ToAccountId,
			Amount: e.Amount,
			ForexRate: e.ForexRate,
			Description: e.Description,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountTransferCredited e => new AccountTransferCreditedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			FromAccountId: e.FromAccountId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountTransferRefunded e => new AccountTransferRefundedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			Amount: e.Amount,
			Description: e.Description,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		AccountBalanceAdjusted e => new AccountBalanceAdjustedEvent(
			EventId: e.Id,
			AccountId: e.AccountId,
			SourceId: e.SourceId,
			SourceType: e.SourceType,
			OldRate: e.OldRate,
			NewRate: e.NewRate,
			Amount: e.Amount,
			Delta: e.Delta,
			Version: e.Version,
			OccurredAt: e.OccurredAt
		),
		_ => ExecuteDefaultCase(@event: @event)
	};

	private IAccountIntegrationEvent? ExecuteDefaultCase(IEvent @event)
	{
		logger.ZLogWarning(message: 
			$"[IntegrationEventMapper] No integration event mapping defined for domain event '{@event.GetType().Name}'. " +
			$"The event will not be published to the outbox. Add a mapping if outbox propagation is required."
		);
		return null;
	}
}