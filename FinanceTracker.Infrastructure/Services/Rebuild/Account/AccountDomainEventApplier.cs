using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.Account;

namespace FinanceTracker.Infrastructure.Services.Rebuild.Account;

/// <summary>
/// Applies individual account domain events to the read model projection
/// by dispatching each event to the corresponding <see cref="IAccountWriteRepository"/> method.
/// Used by <see cref="AccountProjectionRebuilder"/> and the account projection worker.
/// </summary>
public sealed class AccountDomainEventApplier(IAccountWriteRepository repository)
{
	public Task ApplyAsync(IEvent @event, CancellationToken ct) => @event switch
	{
		AccountCreated e => repository.CreateAsync(@event: e, ct: ct),
		AccountDebited e => repository.DebitAsync(@event: e, ct: ct),
		AccountCredited e => repository.CreditAsync(@event: e, ct: ct),
		AccountRenamed e => repository.RenameAsync(@event: e, ct: ct),
		AccountArchived e => repository.ArchiveAsync(@event: e, ct: ct),
		AccountUnarchived e => repository.UnarchiveAsync(@event: e, ct: ct),
		AccountTransferDebited e => repository.TransferDebitAsync(@event: e, ct: ct),
		AccountTransferCredited e => repository.TransferCreditAsync(@event: e, ct: ct),
		AccountTransferRefunded e => repository.RefundTransferAsync(@event: e, ct: ct),
		AccountBalanceAdjusted e => repository.AdjustBalanceAsync(@event: e, ct: ct),
		_ => throw new UnknownEventException(message: $"Unhandled account domain event: {@event.GetType().Name}", eventType: @event.GetType())
	};
}
