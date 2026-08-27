using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Events.Account;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Worker.AccountProjection.Projection;

/// <summary>
/// Applies individual account integration events to the read model projection
/// by dispatching each to the corresponding <see cref="IAccountWriteRepository"/> method.
/// Used exclusively by <see cref="AccountProjection"/>.
/// </summary>
public sealed class AccountEventApplier(IAccountWriteRepository repository)
{
	public Task ApplyAsync(IIntegrationEvent @event, CancellationToken ct = default) => @event switch
	{
		AccountCreatedEvent e => ApplyAsync(e: e, ct: ct),
		AccountDebitedEvent e => ApplyAsync(e: e, ct: ct),
		AccountCreditedEvent e => ApplyAsync(e: e, ct: ct),
		AccountTransactionRevertedEvent e => ApplyAsync(e: e, ct: ct),
		AccountRenamedEvent e => ApplyAsync(e: e, ct: ct),
		AccountArchivedEvent e => ApplyAsync(e: e, ct: ct),
		AccountUnarchivedEvent e => ApplyAsync(e: e, ct: ct),
		AccountTransferDebitedEvent e => ApplyAsync(e: e, ct: ct),
		AccountTransferCreditedEvent e => ApplyAsync(e: e, ct: ct),
		AccountTransferRefundedEvent e => ApplyAsync(e: e, ct: ct),
		AccountBalanceAdjustedEvent e => ApplyAsync(e: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled integration event: {@event.GetType().Name}", eventType: @event.GetType())
	};

	private Task ApplyAsync(AccountCreatedEvent e, CancellationToken ct) => repository.CreateAsync(new AccountCreated(
		Id: e.EventId,
		AccountId: e.AccountId,
		UserId: e.UserId,
		Name: Name.Reconstitute(value: e.Name),
		Type: Enum.Parse<AccountType>(value: e.AccountType),
		Currency: Currency.Reconstitute(value: e.Currency),
		Balance: e.Balance,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountDebitedEvent e, CancellationToken ct) => repository.DebitAsync(new AccountDebited(
		Id: e.EventId,
		AccountId: e.AccountId,
		TransactionId: e.TransactionId,
		CategoryId: e.CategoryId,
		Amount: e.Amount,
		ExchangeRate: e.ExchangeRate,
		Description: e.Description,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountCreditedEvent e, CancellationToken ct) => repository.CreditAsync(new AccountCredited(
		Id: e.EventId,
		AccountId: e.AccountId,
		TransactionId: e.TransactionId,
		CategoryId: e.CategoryId,
		Amount: e.Amount,
		ExchangeRate: e.ExchangeRate,
		Description: e.Description,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountTransactionRevertedEvent e, CancellationToken ct) => repository.RevertTransactionAsync(new AccountTransactionReverted(
		Id: e.EventId,
		AccountId: e.AccountId,
		TransactionId: e.TransactionId,
		CategoryId: e.CategoryId,
		Amount: e.Amount,
		ExchangeRate: e.ExchangeRate,
		Direction: e.Direction,
		Description: e.Description,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountRenamedEvent e, CancellationToken ct) => repository.RenameAsync(new AccountRenamed(
		Id: e.EventId,
		AccountId: e.AccountId,
		NewName: Name.Reconstitute(value: e.NewName),
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountArchivedEvent e, CancellationToken ct) => repository.ArchiveAsync(new AccountArchived(
		Id: e.EventId,
		AccountId: e.AccountId,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountUnarchivedEvent e, CancellationToken ct) => repository.UnarchiveAsync(new AccountUnarchived(
		Id: e.EventId,
		AccountId: e.AccountId,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountTransferDebitedEvent e, CancellationToken ct) => repository.TransferDebitAsync(new AccountTransferDebited(
		Id: e.EventId,
		AccountId: e.AccountId,
		TransferId: e.TransferId,
		ToAccountId: e.ToAccountId,
		Amount: e.Amount,
		ForexRate: e.ForexRate,
		Description: e.Description,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountTransferCreditedEvent e, CancellationToken ct) => repository.TransferCreditAsync(new AccountTransferCredited(
		Id: e.EventId,
		AccountId: e.AccountId,
		TransferId: e.TransferId,
		FromAccountId: e.FromAccountId,
		Amount: e.Amount,
		ExchangeRate: e.ExchangeRate,
		Description: e.Description,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountTransferRefundedEvent e, CancellationToken ct) => repository.RefundTransferAsync(new AccountTransferRefunded(
		Id: e.EventId,
		AccountId: e.AccountId,
		TransferId: e.TransferId,
		Amount: e.Amount,
		Description: e.Description,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);

	private Task ApplyAsync(AccountBalanceAdjustedEvent e, CancellationToken ct) => repository.AdjustBalanceAsync(new AccountBalanceAdjusted(
		Id: e.EventId,
		AccountId: e.AccountId,
		SourceId: e.SourceId,
		SourceType: e.SourceType,
		OldRate: e.OldRate,
		NewRate: e.NewRate,
		Amount: e.Amount,
		Delta: e.Delta,
		Version: e.Version,
		OccurredAt: e.OccurredAt
	), ct);
}
