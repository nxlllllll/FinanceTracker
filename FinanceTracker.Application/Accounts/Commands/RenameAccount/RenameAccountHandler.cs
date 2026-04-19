using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.RenameAccount;

public sealed class RenameAccountHandler(
	IAccountRepository accountRepository,
	IPublisher publisher
) : IRequestHandler<RenameAccountCommand>
{
	public async Task Handle(
		RenameAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct)
			?? throw new InvalidOperationException($"Account with id {command.AccountId} not found");
		
		account.Rename(newName: command.NewName);

		List<IEvent> events = account.Events.ToList();
		await accountRepository.SaveAsync(account: account, ct: ct);

		await publisher.Publish(
			notification: new AccountEventsNotification(AccountId: account.Id, Events: events),
			cancellationToken: ct
		);
	}
}