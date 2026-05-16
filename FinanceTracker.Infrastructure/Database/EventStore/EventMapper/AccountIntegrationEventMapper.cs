using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;

namespace FinanceTracker.Infrastructure.Database.EventStore.EventMapper;

public sealed class AccountIntegrationEventMapper : IIntegrationEventMapper
{
	public IAccountIntegrationEvent? Map(IEvent domainEvent) => domainEvent switch
	{
		AccountCreated e => new AccountCreatedEvent(
			AccountId: e.AccountId,
			UserId: e.UserId,
			Name: e.Name,
			AccountType: e.Type.ToString(),
			Currency: e.Currency,
			Balance: e.Balance,
			OccurredAt: e.OccurredAt
		),
		AccountDebited e => new AccountDebitedEvent(
			AccountId: e.AccountId,
			TransactionId: e.TransactionId,
			CategoryId: e.CategoryId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		),
		AccountCredited e => new AccountCreditedEvent(
			AccountId: e.AccountId,
			TransactionId: e.TransactionId,
			CategoryId: e.CategoryId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		),
		AccountRenamed e => new AccountRenamedEvent(
			AccountId: e.AccountId,
			NewName: e.NewName,
			OccurredAt: e.OccurredAt
		),
		AccountArchived e => new AccountArchivedEvent(
			AccountId: e.AccountId,
			OccurredAt: e.OccurredAt
		),
		AccountUnarchived e => new AccountUnarchivedEvent(
			AccountId: e.AccountId,
			OccurredAt: e.OccurredAt
		),
		AccountTransferDebited e => new AccountTransferDebitedEvent(
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			ToAccountId: e.ToAccountId,
			Amount: e.Amount,
			ForexRate: e.ForexRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		),
		AccountTransferCredited e => new AccountTransferCreditedEvent(
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			FromAccountId: e.FromAccountId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		),
		AccountTransferRefunded e => new AccountTransferRefundedEvent(
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			Amount: e.Amount,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		),
		AccountBalanceAdjusted e => new AccountBalanceAdjustedEvent(
			AccountId: e.AccountId,
			SourceId: e.SourceId,
			SourceType: e.SourceType,
			OldRate: e.OldRate,
			NewRate: e.NewRate,
			Amount: e.Amount,
			Delta: e.Delta,
			OccurredAt: e.OccurredAt
		),
		_ => throw new UnknownEventException(message: "Event is unknown.", eventType: domainEvent.GetType())
	};
}