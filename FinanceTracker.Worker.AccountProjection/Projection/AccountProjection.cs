using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Worker.AccountProjection.Projection.Notifications;
using MediatR;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Projection;

public sealed class AccountProjection(
	IAccountWriteRepository accountWriteRepository,
	ILogger<AccountProjection> logger
) : INotificationHandler<AccountEventsNotification>
{
	private async Task HandleAsync(IEvent @event, CancellationToken ct)
	{
		Task task = @event switch
		{
			AccountCreated e => HandleAsync(@event: e, ct: ct),
			AccountRenamed e => HandleAsync(@event: e, ct: ct),
			AccountArchived e => HandleAsync(@event: e, ct: ct),
			AccountUnarchived e => HandleAsync(@event: e, ct: ct),
			AccountDebited e => HandleAsync(@event: e, ct: ct),
			AccountCredited e => HandleAsync(@event: e, ct: ct),
			AccountTransferDebited e => HandleAsync(@event: e, ct: ct),
			AccountTransferCredited e => HandleAsync(@event: e, ct: ct),
			AccountBalanceAdjusted e => HandleAsync(@event: e, ct: ct),
			_ => throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType())
		};
		await task;
		logger.ZLogDebug(message: $"Projected event {@event.GetType().Name}.");
	}

	private async Task HandleAsync(AccountCreated @event, CancellationToken ct)
		=> await accountWriteRepository.CreateAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountRenamed @event, CancellationToken ct)
		=> await accountWriteRepository.RenameAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountArchived @event, CancellationToken ct)
		=> await accountWriteRepository.ArchiveAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountUnarchived @event, CancellationToken ct)
		=> await accountWriteRepository.UnarchiveAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountDebited @event, CancellationToken ct)
		=> await accountWriteRepository.DebitAsync(@event: @event, ct: ct);
	
	private async Task HandleAsync(AccountCredited @event, CancellationToken ct)
		=> await accountWriteRepository.CreditAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountTransferDebited @event, CancellationToken ct)
		=> await accountWriteRepository.TransferDebitAsync(@event: @event, ct: ct);
	
	private async Task HandleAsync(AccountTransferCredited @event, CancellationToken ct)
		=> await accountWriteRepository.TransferCreditAsync(@event: @event, ct: ct);
	
	private async Task HandleAsync(AccountBalanceAdjusted @event, CancellationToken ct)
		=> await accountWriteRepository.AdjustBalanceAsync(@event: @event, ct: ct);
	
	public async Task Handle(AccountEventsNotification notification, CancellationToken ct = default)
	{
		foreach (IEvent @event in notification.Events)
			await HandleAsync(@event: @event, ct: ct);
	}
}