using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;

public sealed class UnarchiveAccountHandler(
	IAccountRepository accountRepository,
	IPublisher publisher
) : IRequestHandler<UnarchiveAccountCommand>
{
	public async Task Handle(
		UnarchiveAccountCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(command.AccountId, ct)
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		account.Unarchive();

		List<IEvent> events = [..account.Events];
		await accountRepository.SaveAsync(account: account, ct: ct);

		await publisher.Publish(
			notification: new AccountEventsNotification(AccountId: account.Id, Events: events),
			cancellationToken: ct
		);
	}
}