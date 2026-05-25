using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Worker.AccountProjection.Projection;

public sealed class AccountEventApplier(IAccountWriteRepository repository)
{
	public Task ApplyAsync(IAccountIntegrationEvent @event, CancellationToken ct) => @event switch
	{
		AccountCreatedEvent e => repository.CreateAsync(new AccountCreated(
			Id: e.EventId,
			AccountId: e.AccountId,
			UserId: e.UserId,
			Name: Name.Reconstitute(value: e.Name),
			Type: Enum.Parse<AccountType>(value: e.AccountType),
			Currency: Currency.Reconstitute(value: e.Currency),
			Balance: e.Balance,
			OccurredAt: e.OccurredAt
		), ct),
		AccountDebitedEvent e => repository.DebitAsync(new AccountDebited(
			Id: e.EventId,
			AccountId: e.AccountId,
			TransactionId: e.TransactionId,
			CategoryId: e.CategoryId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		), ct),
		AccountCreditedEvent e => repository.CreditAsync(new AccountCredited(
			Id: e.EventId,
			AccountId: e.AccountId,
			TransactionId: e.TransactionId,
			CategoryId: e.CategoryId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		), ct),
		AccountRenamedEvent e => repository.RenameAsync(new AccountRenamed(
			Id: e.EventId,
			AccountId: e.AccountId,
			NewName: Name.Reconstitute(value: e.NewName),
			OccurredAt: e.OccurredAt
		), ct),
		AccountArchivedEvent e => repository.ArchiveAsync(new AccountArchived(
			Id: e.EventId,
			AccountId: e.AccountId,
			OccurredAt: e.OccurredAt
		), ct),
		AccountUnarchivedEvent e => repository.UnarchiveAsync(new AccountUnarchived(
			Id: e.EventId,
			AccountId: e.AccountId,
			OccurredAt: e.OccurredAt
		), ct),
		AccountTransferDebitedEvent e => repository.TransferDebitAsync(new AccountTransferDebited(
			Id: e.EventId,
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			ToAccountId: e.ToAccountId,
			Amount: e.Amount,
			ForexRate: e.ForexRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		), ct),
		AccountTransferCreditedEvent e => repository.TransferCreditAsync(new AccountTransferCredited(
			Id: e.EventId,
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			FromAccountId: e.FromAccountId,
			Amount: e.Amount,
			ExchangeRate: e.ExchangeRate,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		), ct),
		AccountTransferRefundedEvent e => repository.RefundTransferAsync(new AccountTransferRefunded(
			Id: e.EventId,
			AccountId: e.AccountId,
			TransferId: e.TransferId,
			Amount: e.Amount,
			Description: e.Description,
			OccurredAt: e.OccurredAt
		), ct),
		AccountBalanceAdjustedEvent e => repository.AdjustBalanceAsync(new AccountBalanceAdjusted(
			Id: e.EventId,
			AccountId: e.AccountId,
			SourceId: e.SourceId,
			SourceType: e.SourceType,
			OldRate: e.OldRate,
			NewRate: e.NewRate,
			Amount: e.Amount,
			Delta: e.Delta,
			OccurredAt: e.OccurredAt
		), ct),
		_ => throw new InvalidOperationException(message: $"Unhandled integration event: {@event.GetType().Name}")
	};
}
